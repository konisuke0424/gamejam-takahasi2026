// Browser-side helpers for the WebGL build, so the game behaves the same on any hosting page
// (our own template, itch.io, unityroom) rather than relying on the page's own script.
mergeInto(LibraryManager.library, {
  // The right button walks backward and the middle button is unused: keep the browser's context
  // menu and middle-click auto-scroll off the game canvas.
  KaiForest_BlockContextMenu: function () {
    var canvas = Module['canvas'] || document.querySelector('canvas');
    if (!canvas || canvas.__kaiForestNoMenu) return;
    canvas.__kaiForestNoMenu = true;
    canvas.addEventListener('contextmenu', function (e) { e.preventDefault(); });
    canvas.addEventListener('mousedown', function (e) { if (e.button === 1) e.preventDefault(); });
    window.addEventListener('auxclick', function (e) { if (e.button === 1) e.preventDefault(); });
  }
});
