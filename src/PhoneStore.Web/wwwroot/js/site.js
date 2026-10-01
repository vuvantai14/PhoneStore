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
