document.querySelectorAll('[data-product-image]').forEach(image => {
    const usePlaceholder = () => {
        if (!image.dataset.fallback) {
            image.dataset.fallback = 'true';
            image.src = '/images/product-placeholder.svg';
        }
    };
    image.addEventListener('error', usePlaceholder);
    if (image.complete && image.naturalWidth === 0) usePlaceholder();
});
const filters = document.querySelector('.filter-panel');
if (filters) {
    const mobile = window.matchMedia('(max-width: 700px)');
    const adaptFilters = () => { filters.open = !mobile.matches; };
    adaptFilters();
    mobile.addEventListener('change', adaptFilters);
}

document.querySelectorAll('[data-gallery]').forEach(gallery => {
    const mainImage = gallery.querySelector('#gallery-image');
    const thumbnails = gallery.querySelectorAll('[data-gallery-thumbnail]');
    thumbnails.forEach(thumbnail => {
        thumbnail.addEventListener('click', () => {
            delete mainImage.dataset.fallback;
            mainImage.alt = thumbnail.dataset.alt;
            mainImage.src = thumbnail.dataset.image;
            thumbnails.forEach(item => item.setAttribute('aria-pressed', String(item === thumbnail)));
        });
    });
});

// Presentation-only form feedback. Native POST, antiforgery and checkout token stay intact.
document.querySelectorAll('form[data-ui-submit]').forEach(form => {
    let pending = false;
    const buttons = [...form.querySelectorAll('button[type="submit"]')];
    const originalLabels = buttons.map(button => button.innerHTML);
    const summary = form.querySelector('[data-error-summary]');
    const fields = [...form.querySelectorAll('input:not([type="hidden"]), textarea, select')];
    const fieldError = field => (field.getAttribute('aria-describedby') || '').split(/\s+/)
        .map(id => document.getElementById(id)).find(element => element?.classList.contains('field-error'));
    let invalidScheduled = false;

    const showErrors = () => {
        invalidScheduled = false;
        if (!summary) return;
        const invalid = fields.filter(field => !field.validity.valid);
        if (!invalid.length) return;
        const list = document.createElement('ul');
        invalid.forEach(field => {
            const label = form.querySelector(`label[for="${field.id}"]`);
            const name = label?.textContent.replace('*', '').trim() || 'Thông tin';
            const message = field.validity.valueMissing ? `Vui lòng nhập ${name.toLowerCase()}.` : field.validationMessage;
            const error = fieldError(field);
            if (error) error.textContent = message;
            field.setAttribute('aria-invalid', 'true');
            const item = document.createElement('li');
            const link = document.createElement('a');
            link.href = `#${field.id}`;
            link.textContent = message;
            item.append(link);
            list.append(item);
        });
        summary.replaceChildren(list);
        summary.classList.remove('validation-summary-valid');
        summary.classList.add('validation-summary-errors');
        summary.focus();
    };

    if (summary) {
        if (summary.classList.contains('validation-summary-errors')) {
            // Link server-side field errors without exposing or changing posted values.
            summary.querySelectorAll('li').forEach(item => {
                const field = fields.find(input => {
                    const error = fieldError(input);
                    return error?.textContent.trim() && error.textContent.trim() === item.textContent.trim();
                });
                if (field) {
                    field.setAttribute('aria-invalid', 'true');
                    const link = document.createElement('a');
                    link.href = `#${field.id}`;
                    link.textContent = item.textContent;
                    item.replaceChildren(link);
                }
            });
            summary.focus();
        }
        form.addEventListener('invalid', event => {
            event.preventDefault();
            if (!invalidScheduled) {
                invalidScheduled = true;
                queueMicrotask(showErrors);
            }
        }, true);
        fields.forEach(field => field.addEventListener('input', () => {
            if (!field.validity.valid) return;
            field.removeAttribute('aria-invalid');
            const error = fieldError(field);
            if (error) error.textContent = '';
        }));
    }

    form.addEventListener('submit', event => {
        if (pending || event.defaultPrevented) {
            if (pending) event.preventDefault();
            return;
        }
        if (form.dataset.confirmCancel && !window.confirm(form.dataset.confirmCancel)) {
            event.preventDefault();
            return;
        }
        // Wait until all submit listeners have accepted the event before showing busy state.
        queueMicrotask(() => {
            if (event.defaultPrevented) return;
            pending = true;
            form.setAttribute('aria-busy', 'true');
            buttons.forEach(button => {
                button.disabled = true;
                button.textContent = form.dataset.pendingLabel || 'Đang xử lý…';
            });
        });
    });
    window.addEventListener('pageshow', () => {
        pending = false;
        form.removeAttribute('aria-busy');
        buttons.forEach((button, index) => {
            button.disabled = false;
            button.innerHTML = originalLabels[index];
        });
    });
});

// S29.9I.1 — Add to cart without redirect (AJAX + Toast + Header badge sync)
(function () {
    let toastTimer = null;

    function showCartToast(message, type) {
        type = type || 'success';
        let container = document.getElementById('cart-toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'cart-toast-container';
            container.className = 'cart-toast-container';
            container.setAttribute('role', 'region');
            container.setAttribute('aria-label', 'Thông báo giỏ hàng');
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = 'cart-toast cart-toast-' + type;
        toast.setAttribute('role', type === 'error' ? 'alert' : 'status');
        toast.setAttribute('aria-live', 'polite');

        const iconHtml = type === 'success'
            ? '<svg class="cart-toast-icon-svg" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10" fill="#eaf7ee" stroke="#246546" stroke-width="1.8"/><path d="m8.5 12.5 2.5 2.5 5-5" stroke="#246546" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>'
            : '<svg class="cart-toast-icon-svg" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10" fill="#fdf0f0" stroke="#a52f3a" stroke-width="1.8"/><path d="M12 8v5M12 16h.01" stroke="#a52f3a" stroke-width="2" stroke-linecap="round"/></svg>';

        const actionHtml = type === 'success'
            ? '<a href="/cart" class="cart-toast-link">Xem giỏ hàng <span aria-hidden="true">→</span></a>'
            : '';

        toast.innerHTML =
            '<div class="cart-toast-content">' +
                iconHtml +
                '<span class="cart-toast-text">' + message + '</span>' +
                actionHtml +
            '</div>' +
            '<button type="button" class="cart-toast-close" aria-label="Đóng thông báo">' +
                '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>' +
            '</button>';

        container.innerHTML = '';
        container.appendChild(toast);

        requestAnimationFrame(function () {
            toast.classList.add('cart-toast-visible');
        });

        const dismiss = function () {
            toast.classList.remove('cart-toast-visible');
            const removeEl = function () {
                if (toast.parentElement) toast.remove();
            };
            toast.addEventListener('transitionend', removeEl, { once: true });
            setTimeout(removeEl, 350);
        };

        const closeBtn = toast.querySelector('.cart-toast-close');
        if (closeBtn) {
            closeBtn.addEventListener('click', dismiss);
        }

        clearTimeout(toastTimer);
        toastTimer = setTimeout(dismiss, 3000);
    }

    function updateCartCount(count) {
        const badge = document.querySelector('.cart-entry b');
        if (badge) {
            badge.textContent = count;
            badge.classList.remove('cart-count-bump');
            void badge.offsetWidth;
            badge.classList.add('cart-count-bump');
        }
        const cartLink = document.querySelector('.cart-entry');
        if (cartLink) {
            cartLink.setAttribute('aria-label', 'Giỏ hàng, ' + count + ' sản phẩm');
        }
    }

    document.addEventListener('submit', async function (event) {
        const form = (event.target && event.target.matches && event.target.matches('form[data-cart-form], form[action="/cart/add"], form[action^="/cart/add"]'))
            ? event.target
            : ((event.target && event.target.closest ? event.target.closest('form[data-cart-form], form[action="/cart/add"], form[action^="/cart/add"]') : null)
                || (event.submitter && event.submitter.closest ? event.submitter.closest('form[data-cart-form], form[action="/cart/add"], form[action^="/cart/add"]') : null));
        if (!form) return;

        // Prevent default browser navigation/redirect
        event.preventDefault();

        // Prevent double submit
        if (form.dataset.submitting === 'true') return;

        const submitBtn = form.querySelector('button[type="submit"]');
        const originalBtnHtml = submitBtn ? submitBtn.innerHTML : '';
        const isDetailPurchase = form.classList.contains('purchase-form');

        try {
            form.dataset.submitting = 'true';
            if (submitBtn) {
                submitBtn.disabled = true;
                if (isDetailPurchase) {
                    submitBtn.innerHTML = 'Đang thêm… <span aria-hidden="true">→</span>';
                } else {
                    submitBtn.innerHTML = '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="M3 3h2l3 12h11l2-8H6"/><circle cx="9" cy="20" r="1"/><circle cx="18" cy="20" r="1"/></svg> Đang thêm…';
                }
            }

            const formData = new FormData(form);
            const response = await fetch(form.action, {
                method: 'POST',
                body: formData,
                headers: {
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            // If backend redirected to login
            if (response.redirected) {
                if (response.url && response.url.toLowerCase().includes('/account/login')) {
                    window.location.href = response.url;
                    return;
                }
            }

            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('text/html')) {
                showCartToast('Không thể thêm vào giỏ hàng. Vui lòng thử lại sau.', 'error');
                return;
            }

            const result = await response.json();

            if (result && result.success) {
                showCartToast(result.message || 'Đã thêm vào giỏ hàng.', 'success');
                if (typeof result.cartCount === 'number') {
                    updateCartCount(result.cartCount);
                }
            } else {
                const errorMsg = (result && result.message) ? result.message : 'Không thể thêm vào giỏ hàng.';
                showCartToast(errorMsg, 'error');
                if (typeof result?.cartCount === 'number') {
                    updateCartCount(result.cartCount);
                }
            }
        } catch (err) {
            showCartToast('Lỗi kết nối máy chủ. Vui lòng thử lại sau.', 'error');
        } finally {
            delete form.dataset.submitting;
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.innerHTML = originalBtnHtml;
            }
        }
    });
})();

// S29.9J.0 — Header Dropdown Navigation (Account & Cart)
(function () {
    const dropdowns = document.querySelectorAll('[data-header-dropdown]');
    if (!dropdowns.length) return;

    function closeDropdown(dropdown) {
        if (!dropdown) return;
        dropdown.removeAttribute('data-open');
        const trigger = dropdown.querySelector('.header-dropdown-trigger');
        if (trigger) {
            trigger.setAttribute('aria-expanded', 'false');
        }
    }

    function openDropdown(dropdown) {
        if (!dropdown) return;
        dropdowns.forEach(d => {
            if (d !== dropdown) closeDropdown(d);
        });
        dropdown.setAttribute('data-open', 'true');
        const trigger = dropdown.querySelector('.header-dropdown-trigger');
        if (trigger) {
            trigger.setAttribute('aria-expanded', 'true');
        }
    }

    function closeAllDropdowns() {
        dropdowns.forEach(closeDropdown);
    }

    dropdowns.forEach(dropdown => {
        const trigger = dropdown.querySelector('.header-dropdown-trigger');
        const menu = dropdown.querySelector('.header-dropdown-menu');
        if (!trigger || !menu) return;

        trigger.addEventListener('click', function (event) {
            event.stopPropagation();
            const isOpen = dropdown.getAttribute('data-open') === 'true';
            if (isOpen) {
                closeDropdown(dropdown);
            } else {
                openDropdown(dropdown);
            }
        });

        // Close on blur when keyboard focus leaves this dropdown
        dropdown.addEventListener('focusout', function (event) {
            if (!dropdown.contains(event.relatedTarget)) {
                closeDropdown(dropdown);
            }
        });

        // When a menu item (link or logout submit) is clicked, close dropdown
        menu.addEventListener('click', function (event) {
            const item = event.target.closest('a, button[type="submit"]');
            if (item) {
                closeDropdown(dropdown);
            }
        });
    });

    // Close on click outside
    document.addEventListener('click', function (event) {
        if (!event.target.closest('[data-header-dropdown]')) {
            closeAllDropdowns();
        }
    });

    // Close on Escape key and return focus to active trigger
    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' || event.key === 'Esc') {
            const openDropdown = document.querySelector('[data-header-dropdown][data-open="true"]');
            if (openDropdown) {
                const trigger = openDropdown.querySelector('.header-dropdown-trigger');
                closeAllDropdowns();
                if (trigger) {
                    trigger.focus();
                }
            }
        }
    });
})();

// S29.9J — Compact Cart Stepper Controls (- / +)
(function () {
    function initCartSteppers() {
        document.querySelectorAll('[data-cart-update-form]').forEach(form => {
            if (form.dataset.stepperInitialized) return;
            form.dataset.stepperInitialized = 'true';

            const input = form.querySelector('.qty-input');
            const minusBtn = form.querySelector('.qty-btn-minus');
            const plusBtn = form.querySelector('.qty-btn-plus');
            if (!input || !minusBtn || !plusBtn) return;

            function submitUpdate() {
                minusBtn.disabled = true;
                plusBtn.disabled = true;
                if (typeof form.requestSubmit === 'function') {
                    form.requestSubmit();
                } else {
                    form.submit();
                }
            }

            minusBtn.addEventListener('click', function (e) {
                e.preventDefault();
                const current = parseInt(input.value, 10) || 1;
                const min = parseInt(input.min, 10) || 1;
                if (current > min) {
                    input.value = current - 1;
                    submitUpdate();
                }
            });

            plusBtn.addEventListener('click', function (e) {
                e.preventDefault();
                const current = parseInt(input.value, 10) || 1;
                const max = parseInt(input.max, 10) || 9999;
                if (current < max) {
                    input.value = current + 1;
                    submitUpdate();
                }
            });

            input.addEventListener('change', function () {
                let val = parseInt(input.value, 10);
                const min = parseInt(input.min, 10) || 1;
                const max = parseInt(input.max, 10) || 9999;
                if (isNaN(val) || val < min) val = min;
                if (val > max) val = max;
                input.value = val;
                submitUpdate();
            });

            input.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    input.blur();
                }
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCartSteppers);
    } else {
        initCartSteppers();
    }
})();
