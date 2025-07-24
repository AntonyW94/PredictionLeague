window.blazorInterop = {
    showConfirm: function(title, text) {
        return new Promise((resolve) => {
            Swal.fire({
                title: title,
                text: text,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#00FF85', // --pl-green
                cancelButtonColor: '#E90052', // --pl-pink
                confirmButtonText: 'Yes, delete it!',
                background: '#4A2E6C', // --pl-light-purple
                color: '#FFFFFF'
            }).then((result) => {
                resolve(result.isConfirmed);
            });
        });
    }
};