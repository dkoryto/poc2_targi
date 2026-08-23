// Stamp the resolved theme before first paint so the app never flashes the wrong palette.
// Kept as an external file (not inline) so it satisfies the production CSP `script-src 'self'`.
(function () {
  try {
    var p = localStorage.getItem('dspc.theme');
    if (p !== 'light' && p !== 'dark') {
      p = window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
    }
    document.documentElement.dataset.theme = p;
  } catch (e) {
    document.documentElement.dataset.theme = 'dark';
  }
})();
