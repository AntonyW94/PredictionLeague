﻿window.blazorInterop = {
    showConfirm: function (title, text, confirmButtonText, cancelButtonText) {
        return new Promise((resolve) => {
            Swal.fire({
                title: title,
                text: text,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#00FF85',
                cancelButtonColor: '#E90052',
                confirmButtonText: confirmButtonText,
                cancelButtonText: cancelButtonText,

                background: '#4A2E6C',
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
    },
    showReassignLeagueConfirm: function (title, userList, userToDeleteId) {
        const optionsHtml = userList
            .filter(user => user.id !== userToDeleteId)
            .map(user => `<option value="${user.id}">${user.fullName}</option>`)
            .join('');

        return new Promise((resolve) => {
            Swal.fire({
                title: title,
                html: `
                    <p class="swal2-text">To delete this account, you must select another user to take ownership of their leagues.</p>
                    <select id="newAdminSelect" class="swal2-select">
                        <option value="">-- Select a user --</option>
                        ${optionsHtml}
                    </select>
                `,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#00FF85',
                cancelButtonColor: '#E90052',
                confirmButtonText: '<i class="bi bi-check-circle"></i> <strong>Confirm Deletion</strong>',
                cancelButtonText: '<i class="bi bi-x-circle"></i> <strong>Cancel</strong>',
                background: '#4A2E6C',
                color: '#FFFFFF',
                preConfirm: () => {
                    // ReSharper disable once Html.IdNotResolved
                    const select = document.getElementById('newAdminSelect');
                    if (select.value) {
                        return select.value;
                    }
                    Swal.showValidationMessage('You must select a new administrator.');
                    return false;
                }
            }).then((result) => {
                if (result.isConfirmed && result.value) {
                    resolve(result.value);
                } else {
                    resolve(null);
                }
            });
        });
    },
    showRoleChangeConfirm: function (userName, currentRole) {
        return new Promise((resolve) => {
            Swal.fire({
                title: `Change role for ${userName}`,
                html: `
                    <div class="swal2-radio-container">
                        <label>
                            <input type="radio" name="swal2-radio" value="Player" ${currentRole === 'Player' ? 'checked' : ''}>
                            <span class="swal2-label-text">Player</span>
                        </label>
                        <label>
                            <input type="radio" name="swal2-radio" value="Administrator" ${currentRole === 'Administrator' ? 'checked' : ''}>
                            <span class="swal2-label-text">Administrator</span>
                        </label>
                    </div>
                `,
                icon: 'question',
                showCancelButton: true,
                confirmButtonColor: '#00FF85',
                cancelButtonColor: '#E90052',
                confirmButtonText: '<i class="bi bi-check-circle"></i> <strong>Save Role</strong>',
                cancelButtonText: '<i class="bi bi-x-circle"></i> <strong>Cancel</strong>',
                background: '#4A2E6C',
                color: '#FFFFFF',
                preConfirm: () => {
                    const selectedRole = Swal.getPopup().querySelector('input[name="swal2-radio"]:checked');
                    if (!selectedRole) {
                        Swal.showValidationMessage('You must select a role.');
                        return false;
                    }
                    return selectedRole.value;
                }
            }).then((result) => {
                if (result.isConfirmed && result.value) {
                    resolve(result.value);
                } else {
                    resolve(null);
                }
            });
        });
    }
};