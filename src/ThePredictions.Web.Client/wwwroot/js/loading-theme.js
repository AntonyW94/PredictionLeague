// Themes the pre-Blazor loading screen. Admin and league pages use the light layout, so the spinner
// shown before the app boots has to match or the first paint flashes the wrong background.
//
// Moved out of an inline <script> in index.html in August 2026: the Content-Security-Policy sets
// script-src 'self' without 'unsafe-inline', so inline blocks are blocked. Must stay a plain
// synchronous script positioned immediately after .app-loading-container, since it reads that element.
(function () {
    var path = window.location.pathname;
    var adminPaths = ['/admin/', '/leagues'];
    var isAdmin = adminPaths.some(function (p) { return path.startsWith(p); });

    if (isAdmin) {
        var container = document.querySelector('.app-loading-container');

        if (container) {
            container.classList.add('app-loading-light');
        }
    }
})();
