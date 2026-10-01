document.querySelectorAll('[data-product-image]').forEach(image => {
    image.addEventListener('error', () => {
        if (!image.dataset.fallback) {
            image.dataset.fallback = 'true';
            image.src = '/images/product-placeholder.svg';
        }
    });
});
const filters = document.querySelector('.filter-panel');
if (filters) {
    const mobile = window.matchMedia('(max-width: 700px)');
    const adaptFilters = () => { filters.open = !mobile.matches; };
    adaptFilters();
    mobile.addEventListener('change', adaptFilters);
}
