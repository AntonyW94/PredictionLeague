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
    },
    showModal: function (id) {
        const modalElement = document.getElementById(id);
        if (modalElement) {
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        }
    },
    hideModal: function (id) {
        const modalElement = document.getElementById(id);
        if (modalElement) {
            const modal = bootstrap.Modal.getInstance(modalElement);
            if (modal) {
                modal.hide();
            }
        }
    }
};