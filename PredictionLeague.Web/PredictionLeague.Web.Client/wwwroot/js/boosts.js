window.boosts = {
    preloadImages: function (urls) {
        if (!urls)
            return;

        const list = [].concat(urls);

        list.forEach(u => {
            if (u && typeof u === 'string') {
                try {
                    const img = new Image();
                    img.src = u;
                } catch (e) {
                }
            }
        });
    }
};