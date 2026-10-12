const menu = document.querySelector('.menu');
const navigation = document.querySelector('#navigation');
function closeMenu() {
  menu?.setAttribute('aria-expanded', 'false');
  menu?.setAttribute('aria-label', 'Abrir menú');
  navigation?.classList.remove('open');
}
menu?.addEventListener('click', () => {
  const open = menu.getAttribute('aria-expanded') !== 'true';
  menu.setAttribute('aria-expanded', String(open));
  menu.setAttribute('aria-label', open ? 'Cerrar menú' : 'Abrir menú');
  navigation.classList.toggle('open', open);
});
document.addEventListener('keydown', event => {
  if (event.key === 'Escape' && menu?.getAttribute('aria-expanded') === 'true') {
    closeMenu();
    menu.focus();
  }
});
matchMedia('(min-width: 701px)').addEventListener('change', closeMenu);
document.querySelector('.skip-main')?.addEventListener('click', () => document.querySelector('main').focus());
const announcement = document.querySelector('#announcement');
let announcementTimer;
function announce(message) {
  clearTimeout(announcementTimer);
  announcement.textContent = message;
  announcementTimer = setTimeout(() => announcement.textContent = '', 4000);
}
document.querySelectorAll('.copy').forEach(button => {
  button.addEventListener('click', async () => {
    const code = button.closest('.code').querySelector('code');
    try {
      await navigator.clipboard.writeText(code.textContent);
      button.textContent = 'Copiado';
      announce('Código copiado al portapapeles.');
      setTimeout(() => button.textContent = 'Copiar ⧉', 1800);
    } catch {
      const range = document.createRange();
      range.selectNodeContents(code);
      getSelection().removeAllRanges();
      getSelection().addRange(range);
      announce('No se pudo acceder al portapapeles. El código está seleccionado para copiarlo manualmente.');
    }
  });
});
