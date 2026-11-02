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
