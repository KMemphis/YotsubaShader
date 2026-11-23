//! ABI C:
//!   int32_t ys_naga_spirv_to_wgsl(const uint8_t* spirv, size_t len, uint8_t** out, size_t* out_len);
//!   void    ys_naga_free(uint8_t* p, size_t len);
//! Devuelve 0 en exito (`out` = WGSL UTF-8) y != 0 en error (`out` = mensaje UTF-8). Siempre liberar con ys_naga_free.

use naga::back::wgsl::{self, WriterFlags};
use naga::valid::{Capabilities, ValidationFlags, Validator};

fn convert(bytes: &[u8]) -> Result<String, String> {
    let module = naga::front::spv::parse_u8_slice(bytes, &naga::front::spv::Options::default())
        .map_err(|e| format!("SPIR-V: {e}"))?;
    let info = Validator::new(ValidationFlags::all(), Capabilities::all())
        .validate(&module)
        .map_err(|e| format!("validacion: {e}"))?;
    wgsl::write_string(&module, &info, WriterFlags::empty()).map_err(|e| format!("WGSL: {e}"))
}

fn give(s: String, out: *mut *mut u8, out_len: *mut usize) {
    let mut b = s.into_bytes().into_boxed_slice();
    unsafe {
        *out_len = b.len();
        *out = b.as_mut_ptr();
    }
    std::mem::forget(b);
}

#[no_mangle]
pub unsafe extern "C" fn ys_naga_spirv_to_wgsl(spirv: *const u8, len: usize, out: *mut *mut u8, out_len: *mut usize) -> i32 {
    if spirv.is_null() || out.is_null() || out_len.is_null() {
        return 2;
    }
    let bytes = std::slice::from_raw_parts(spirv, len);
    match convert(bytes) {
        Ok(s) => { give(s, out, out_len); 0 }
        Err(e) => { give(e, out, out_len); 1 }
    }
}

#[no_mangle]
pub unsafe extern "C" fn ys_naga_free(p: *mut u8, len: usize) {
    if !p.is_null() {
        drop(Box::from_raw(std::slice::from_raw_parts_mut(p, len)));
    }
}
