// Logs unhandled errors and promise rejections to the console, and keeps Blazor's built-in yellow
// error banner hidden - the details in the console are more useful than a banner that cannot be
// dismissed cleanly, and the banner also fires on transient framework-file cache mismatches.
//
// Moved out of an inline <script> in index.html in August 2026: the Content-Security-Policy sets
// script-src 'self' without 'unsafe-inline', so inline blocks are blocked. Must stay ahead of
// blazor.webassembly.js so the handlers are registered before the app can fault.
window.addEventListener("error", function (e) {
    console.error("[Blazor] Unhandled error:", e.message, "\nSource:", e.filename, "Line:", e.lineno, "Col:", e.colno, "\nError:", e.error);

    var ui = document.getElementById("blazor-error-ui");

    if (ui) {
        ui.style.display = "none";
    }
});

window.addEventListener("unhandledrejection", function (e) {
    var detail = e.reason ? (e.reason.stack || e.reason.message || String(e.reason)) : "Unknown rejection";
    console.error("[Blazor] Unhandled promise rejection:", detail);

    var ui = document.getElementById("blazor-error-ui");

    if (ui) {
        ui.style.display = "none";
    }
});

// Blazor sets the banner's display style directly, so watching the attribute is the only way to catch
// it being shown after these handlers have already run.
document.addEventListener("DOMContentLoaded", function () {
    var ui = document.getElementById("blazor-error-ui");

    if (ui) {
        var observer = new MutationObserver(function () {
            if (ui.style.display !== "none") {
                console.warn("[Blazor] Error UI was shown - suppressing banner. Check console for error details above.");
                ui.style.display = "none";
            }
        });

        observer.observe(ui, { attributes: true, attributeFilter: ["style"] });
    }
});
