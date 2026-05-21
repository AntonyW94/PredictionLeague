const countdownTimers = {};

window.blazorInterop = {
    // Reusable helper function to escape HTML entities
    // Use this whenever inserting user-provided text into HTML templates
    escapeHtml: function(unsafe) {
        if (typeof unsafe !== 'string') return '';
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    },
    getTimezoneOffset: function (dateString) {
        if (dateString) {
            return new Date(dateString).getTimezoneOffset();
        }
        return new Date().getTimezoneOffset();
    },
    getWindowWidth: function () {
        return window.innerWidth;
    },
    showConfirm: function (title, text, confirmButtonText, cancelButtonText) {
        return new Promise((resolve) => {
            Swal.fire({
                title: title,
                text: text,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: confirmButtonText,
                cancelButtonText: cancelButtonText,
                customClass: {
                    popup: 'swal2-admin-light',
                    confirmButton: 'swal2-btn-green',
                    cancelButton: 'swal2-btn-red'
                },
                buttonsStyling: false
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
        const self = this;
        const optionsHtml = userList
            .filter(user => user.id !== userToDeleteId)
            .map(user => `<option value="${self.escapeHtml(user.id)}">${self.escapeHtml(user.fullName)}</option>`)
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
                confirmButtonText: '<i class="bi bi-check-circle"></i> <strong>Confirm Deletion</strong>',
                cancelButtonText: '<i class="bi bi-x-circle"></i> <strong>Cancel</strong>',
                customClass: {
                    popup: 'swal2-admin-light',
                    confirmButton: 'swal2-btn-green',
                    cancelButton: 'swal2-btn-red'
                },
                buttonsStyling: false,
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
        const self = this;
        return new Promise((resolve) => {
            Swal.fire({
                title: `Change role for ${self.escapeHtml(userName)}`,
                html: `
                    <div class="swal2-role-cards">
                        <button type="button" class="swal2-role-card ${currentRole === 'Player' ? 'active' : ''}" data-role="Player">
                            <span class="bi bi-controller"></span>
                            <span class="swal2-role-card-label">Player</span>
                        </button>
                        <button type="button" class="swal2-role-card ${currentRole === 'Administrator' ? 'active' : ''}" data-role="Administrator">
                            <span class="bi bi-shield-lock-fill"></span>
                            <span class="swal2-role-card-label">Admin</span>
                        </button>
                    </div>
                    <div id="selectedRole" data-value="${self.escapeHtml(currentRole)}" style="display:none"></div>
                `,
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: '<i class="bi bi-check-circle"></i> <strong>Save Role</strong>',
                cancelButtonText: '<i class="bi bi-x-circle"></i> <strong>Cancel</strong>',
                customClass: {
                    popup: 'swal2-admin-light',
                    confirmButton: 'swal2-btn-green',
                    cancelButton: 'swal2-btn-red'
                },
                buttonsStyling: false,
                didOpen: () => {
                    const popup = Swal.getPopup();
                    popup.querySelectorAll('.swal2-role-card').forEach(card => {
                        card.addEventListener('click', () => {
                            popup.querySelectorAll('.swal2-role-card').forEach(c => c.classList.remove('active'));
                            card.classList.add('active');
                            popup.querySelector('#selectedRole').dataset.value = card.dataset.role;
                        });
                    });
                },
                preConfirm: () => {
                    const value = Swal.getPopup().querySelector('#selectedRole').dataset.value;
                    if (!value) {
                        Swal.showValidationMessage('You must select a role.');
                        return false;
                    }
                    return value;
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
    startCountdown: function (dotNetHelper, methodName, timerId) {
        if (countdownTimers[timerId]) {
            clearInterval(countdownTimers[timerId]);
        }

        countdownTimers[timerId] = setInterval(() => {
            dotNetHelper.invokeMethodAsync(methodName);
        }, 1000);
    },
    stopCountdown: function (timerId) {
        if (countdownTimers[timerId]) {
            clearInterval(countdownTimers[timerId]);
            delete countdownTimers[timerId];
        }
    },
    registerResizeCallback: function (dotNetHelper, methodName) {
        window._resizeHandler = () => {
            dotNetHelper.invokeMethodAsync(methodName, window.innerWidth);
        };
        window.addEventListener('resize', window._resizeHandler);
    },
    unregisterResizeCallback: function () {
        if (window._resizeHandler) {
            window.removeEventListener('resize', window._resizeHandler);
            delete window._resizeHandler;
        }
    },
    updateCarouselHeight: function (trackWrapperId, currentIndex, itemsPerPage) {
        var wrapper = document.getElementById(trackWrapperId);
        if (!wrapper) return;

        var items = wrapper.querySelectorAll('.carousel-item-wrapper');
        var maxHeight = 0;

        // Reset all items to auto height first so we get natural sizes
        items.forEach(function (item) {
            var card = item.querySelector('.card.slide');
            if (card) card.style.minHeight = '';
        });

        // Measure natural heights of visible items
        for (var i = currentIndex; i < currentIndex + itemsPerPage && i < items.length; i++) {
            var content = items[i].querySelector('.carousel-item-content');
            if (content) {
                var height = content.scrollHeight;
                if (height > maxHeight) maxHeight = height;
            }
        }

        // If multiple items visible, make them all the same height
        if (itemsPerPage > 1 && maxHeight > 0) {
            for (var j = currentIndex; j < currentIndex + itemsPerPage && j < items.length; j++) {
                var card = items[j].querySelector('.card.slide');
                if (card) card.style.minHeight = maxHeight + 'px';
            }
        }

        if (maxHeight > 0) {
            wrapper.style.height = maxHeight + 'px';
        }
    },
    scrollToUserRow: function (containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const userRow = container.querySelector('.current-user-highlight');
        if (!userRow) return;

        const containerRect = container.getBoundingClientRect();
        const rowRect = userRow.getBoundingClientRect();
        const scrollTop = userRow.offsetTop - container.offsetTop - (containerRect.height / 2) + (rowRect.height / 2);

        container.scrollTop = Math.max(0, scrollTop);
    },
    scrollActiveRoundIntoView: function (container, smooth) {
        if (!container) return;
        const active = container.querySelector('.round-pill.active');
        if (!active) return;

        active.scrollIntoView({
            inline: 'center',
            block: 'nearest',
            behavior: smooth ? 'smooth' : 'auto'
        });
    }
};