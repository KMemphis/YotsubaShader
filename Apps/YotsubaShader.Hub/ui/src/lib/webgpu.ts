// A small WebGPU renderer for the gallery: draws the WGSL the yotsuba CLI produces. The shaders use one uniform
// struct (Time, Resolution, Mouse, Frame) and build their own positions from the vertex index, so one triangle
// that covers the canvas is all that is needed.

type Member = { name: string; size: number; offset: number };
type Layout = { group: number; binding: number; size: number; members: Member[] };

const SIZES: Record<string, [size: number, align: number]> = {
  f32: [4, 4], i32: [4, 4], u32: [4, 4],
  'vec2<f32>': [8, 8], 'vec2<i32>': [8, 8], 'vec2<u32>': [8, 8],
  'vec3<f32>': [12, 16], 'vec3<i32>': [12, 16], 'vec3<u32>': [12, 16],
  'vec4<f32>': [16, 16], 'vec4<i32>': [16, 16], 'vec4<u32>': [16, 16],
  'mat3x3<f32>': [48, 16], 'mat4x4<f32>': [64, 16],
};
const round = (n: number, a: number) => Math.ceil(n / a) * a;

/** Finds the uniform struct the shader reads and works out where each member sits (WGSL layout rules). */
export function readLayout(wgsl: string): { layout: Layout | null; error?: string } {
  const others = [...wgsl.matchAll(/@group\(\d+\)\s*@binding\(\d+\)\s*var(?:<(\w+)>)?\s+\w+\s*:\s*([^;]+);/g)];
  const uniform = others.find((m) => m[1] === 'uniform');
  const unsupported = others.find((m) => m[1] !== 'uniform' && m[1] !== undefined);
  if (unsupported) return { layout: null, error: `The shader uses ${unsupported[2].trim()}, and textures or buffers are not available in the live preview yet.` };
  if (!uniform) return { layout: null };

  const decl = wgsl.match(/@group\((\d+)\)\s*@binding\((\d+)\)\s*var<uniform>\s+\w+\s*:\s*(\w+)\s*;/)!;
  const body = wgsl.match(new RegExp(`struct\\s+${decl[3]}\\s*\\{([^}]*)\\}`))?.[1];
  if (body === undefined) return { layout: null, error: `Could not find the struct ${decl[3]}.` };

  const members: Member[] = [];
  let offset = 0;
  for (const field of body.split(',').map((f) => f.trim()).filter(Boolean)) {
    const m = field.match(/^(?:@\w+(?:\([^)]*\))?\s*)*(\w+)\s*:\s*(.+)$/);
    const spec = m && SIZES[m[2].replace(/\s+/g, '')];
    if (!m || !spec) return { layout: null, error: `Unsupported uniform member: ${field}` };
    offset = round(offset, spec[1]);
    members.push({ name: m[1], size: spec[0], offset });
    offset += spec[0];
  }
  return { layout: { group: +decl[1], binding: +decl[2], size: Math.max(16, round(offset, 16)), members } };
}

/** Image for each texture a shader may declare, by variable name (the same pairing the site playground uses). */
const TEXTURES: Record<string, string> = {
  albedo: 'red-brick', height: 'red-brick-height', tiles: 'marble',
};
const textureFile = (name: string) => `/shaders/tex/${TEXTURES[name.toLowerCase()] ?? 'marble'}.webp`;

/** Textures and samplers declared at module level: `var Name: texture_2d<f32>;` and `var Name: sampler;`. */
function readResources(wgsl: string) {
  const out: { group: number; binding: number; name: string; kind: 'texture' | 'sampler' }[] = [];
  for (const m of wgsl.matchAll(/@group\((\d+)\)\s*@binding\((\d+)\)\s*var\s+(\w+)\s*:\s*(texture_2d<f32>|sampler)\s*;/g))
    out.push({ group: +m[1], binding: +m[2], name: m[3], kind: m[4] === 'sampler' ? 'sampler' : 'texture' });
  return out;
}

const BLIT = `
@group(0) @binding(0) var scene: texture_2d<f32>;
@vertex fn vs(@builtin(vertex_index) i: u32) -> @builtin(position) vec4<f32> {
  let p = vec2<f32>(f32((i << 1u) & 2u), f32(i & 2u));
  return vec4<f32>(p * 2.0 - 1.0, 0.0, 1.0);
}
@fragment fn fs(@builtin(position) pos: vec4<f32>) -> @location(0) vec4<f32> {
  let size = vec2<i32>(textureDimensions(scene));
  return textureLoad(scene, vec2<i32>(i32(pos.x), size.y - 1 - i32(pos.y)), 0);
}`;

export class ShaderRenderer {
  private pipeline: GPURenderPipeline | null = null;
  private bind: GPUBindGroup | null = null;
  private buffer: GPUBuffer | null = null;
  private layout: Layout | null = null;
  private group = 0;
  private textures = new Map<string, GPUTextureView>();
  private sampler!: GPUSampler;
  // The shaders are written with the origin at the bottom left (as in GL), but WebGPU puts it at the top left. They draw into
  // `scene` and a small pass copies it to the canvas upside down, which is the same as evaluating them with a flipped y.
  private scene: GPUTexture | null = null;
  private blit!: GPURenderPipeline;
  private blitBind: GPUBindGroup | null = null;
  private ctx: GPUCanvasContext;
  private format: GPUTextureFormat;
  private raf = 0;
  private t0 = performance.now();
  private frame = 0;
  private mouse = [0, 0, 0, 0];
  private observer: ResizeObserver;

  private constructor(private canvas: HTMLCanvasElement, private device: GPUDevice) {
    this.ctx = canvas.getContext('webgpu')!;
    this.sampler = device.createSampler({ magFilter: 'linear', minFilter: 'linear', mipmapFilter: 'linear', addressModeU: 'repeat', addressModeV: 'repeat' });
    this.format = navigator.gpu.getPreferredCanvasFormat();
    this.ctx.configure({ device, format: this.format, alphaMode: 'opaque' });
    this.blit = device.createRenderPipeline({
      layout: 'auto',
      vertex: { module: device.createShaderModule({ code: BLIT }), entryPoint: 'vs' },
      fragment: { module: device.createShaderModule({ code: BLIT }), entryPoint: 'fs', targets: [{ format: this.format }] },
    });
    this.observer = new ResizeObserver(() => this.resize());
    this.observer.observe(canvas);
    this.resize();
    const rect = () => canvas.getBoundingClientRect();
    canvas.addEventListener('pointermove', (e) => {
      const r = rect(), k = canvas.width / r.width;
      this.mouse[0] = (e.clientX - r.left) * k; this.mouse[1] = canvas.height - (e.clientY - r.top) * k;
    });
    canvas.addEventListener('pointerdown', () => { this.mouse[2] = this.mouse[0]; this.mouse[3] = this.mouse[1]; });
    device.lost.then(() => cancelAnimationFrame(this.raf));
  }

  /** A renderer, or the reason there is none (no WebGPU in this window, or no adapter). */
  static async create(canvas: HTMLCanvasElement): Promise<ShaderRenderer | string> {
    if (!('gpu' in navigator)) return 'WebGPU is not available in this window.';
    const adapter = await navigator.gpu.requestAdapter();
    if (!adapter) return 'No graphics adapter supports WebGPU on this computer.';
    return new ShaderRenderer(canvas, await adapter.requestDevice());
  }

  private resize() {
    const k = Math.min(window.devicePixelRatio || 1, 2);
    const w = Math.max(1, Math.round(this.canvas.clientWidth * k)), h = Math.max(1, Math.round(this.canvas.clientHeight * k));
    if (this.canvas.width !== w || this.canvas.height !== h) { this.canvas.width = w; this.canvas.height = h; }
  }

  /** Loads the image for a texture variable once; returns its view or why it could not load. */
  private async texture(name: string): Promise<GPUTextureView | string> {
    const key = textureFile(name);
    const have = this.textures.get(key);
    if (have) return have;
    try {
      const blob = await (await fetch(key)).blob();
      const bitmap = await createImageBitmap(blob, { colorSpaceConversion: 'none' });
      const tex = this.device.createTexture({
        size: [bitmap.width, bitmap.height], format: 'rgba8unorm',
        usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST | GPUTextureUsage.RENDER_ATTACHMENT,
      });
      this.device.queue.copyExternalImageToTexture({ source: bitmap }, { texture: tex }, [bitmap.width, bitmap.height]);
      const view = tex.createView();
      this.textures.set(key, view);
      return view;
    } catch (e) {
      return `Could not load the texture for ${name}: ${e instanceof Error ? e.message : e}`;
    }
  }

  /** Builds the pipeline for a new WGSL module. Returns what is wrong with it, or null; the last good one keeps drawing. */
  async setShader(wgsl: string): Promise<string | null> {
    const entry = (stage: string) => wgsl.match(new RegExp(`@${stage}\\s+fn\\s+(\\w+)`))?.[1];
    const vs = entry('vertex'), fs = entry('fragment');
    if (!vs || !fs) return 'The shader needs a vertex and a fragment entry point.';
    const { layout, error } = readLayout(wgsl);
    if (error) return error;

    const d = this.device;
    d.pushErrorScope('validation');
    const module = d.createShaderModule({ code: wgsl });
    const info = await module.getCompilationInfo();
    const bad = info.messages.filter((m) => m.type === 'error');
    if (bad.length) { await d.popErrorScope(); return bad.map((m) => `${m.lineNum}:${m.linePos} ${m.message}`).join('\n'); }

    const pipeline = d.createRenderPipeline({
      layout: 'auto',
      vertex: { module, entryPoint: vs },
      fragment: { module, entryPoint: fs, targets: [{ format: this.format }] },
      primitive: { topology: 'triangle-list' },
    });
    let buffer: GPUBuffer | null = null, bind: GPUBindGroup | null = null;
    const resources = readResources(wgsl);
    if (layout || resources.length) {
      const group = layout?.group ?? resources[0].group;
      const entries: GPUBindGroupEntry[] = [];
      if (layout) {
        buffer = d.createBuffer({ size: layout.size, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST });
        entries.push({ binding: layout.binding, resource: { buffer } });
      }
      for (const r of resources) {
        if (r.group !== group) { await d.popErrorScope(); return 'The live preview only supports one bind group.'; }
        if (r.kind === 'sampler') entries.push({ binding: r.binding, resource: this.sampler });
        else {
          const view = await this.texture(r.name);
          if (typeof view === 'string') { await d.popErrorScope(); return view; }
          entries.push({ binding: r.binding, resource: view });
        }
      }
      bind = d.createBindGroup({ layout: pipeline.getBindGroupLayout(group), entries });
    }
    const failure = await d.popErrorScope();
    if (failure) { buffer?.destroy(); return failure.message; }

    this.buffer?.destroy();
    Object.assign(this, { pipeline, bind, buffer, layout, group: layout?.group ?? resources[0]?.group ?? 0 });
    if (!this.raf) this.loop();
    return null;
  }

  private loop = () => {
    this.raf = requestAnimationFrame(this.loop);
    if (!document.hidden) this.drawFrame((performance.now() - this.t0) / 1000);
  };

  /** Draws one frame as if `time` seconds had passed. Also used to render still thumbnails. */
  drawFrame(time: number) {
    if (!this.pipeline) return;
    this.resize();
    const d = this.device;
    if (this.layout && this.buffer) {
      const data = new ArrayBuffer(this.layout.size), f = new Float32Array(data), u = new Uint32Array(data);
      for (const m of this.layout.members) {
        const i = m.offset / 4;
        switch (m.name.toLowerCase()) {
          case 'time': f[i] = time; break;
          case 'resolution': f[i] = this.canvas.width; f[i + 1] = this.canvas.height; if (m.size > 8) f[i + 2] = 1; break;
          case 'mouse': for (let k = 0; k < Math.min(4, m.size / 4); k++) f[i + k] = this.mouse[k]; break;
          case 'frame': u[i] = this.frame; break;
        }
      }
      d.queue.writeBuffer(this.buffer, 0, data);
    }
    this.frame++;
    if (!this.scene || this.scene.width !== this.canvas.width || this.scene.height !== this.canvas.height) {
      this.scene?.destroy();
      this.scene = d.createTexture({
        size: [this.canvas.width, this.canvas.height], format: this.format,
        usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.TEXTURE_BINDING,
      });
      this.blitBind = d.createBindGroup({ layout: this.blit.getBindGroupLayout(0), entries: [{ binding: 0, resource: this.scene.createView() }] });
    }
    const enc = d.createCommandEncoder();
    const pass = enc.beginRenderPass({
      colorAttachments: [{ view: this.scene.createView(), clearValue: { r: 0, g: 0, b: 0, a: 1 }, loadOp: 'clear', storeOp: 'store' }],
    });
    pass.setPipeline(this.pipeline);
    if (this.bind) pass.setBindGroup(this.group, this.bind);
    pass.draw(3);
    pass.end();
    const out = enc.beginRenderPass({
      colorAttachments: [{ view: this.ctx.getCurrentTexture().createView(), loadOp: 'clear', storeOp: 'store', clearValue: { r: 0, g: 0, b: 0, a: 1 } }],
    });
    out.setPipeline(this.blit);
    out.setBindGroup(0, this.blitBind!);
    out.draw(3);
    out.end();
    d.queue.submit([enc.finish()]);
  }

  restart() { this.t0 = performance.now(); this.frame = 0; }

  destroy() {
    cancelAnimationFrame(this.raf); this.raf = 0;
    this.observer.disconnect();
    this.buffer?.destroy();
    this.scene?.destroy();
    this.device.destroy();
  }
}
