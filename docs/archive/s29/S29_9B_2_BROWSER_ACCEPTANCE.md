# S29.9B.2 — Customer Web browser acceptance

Ngày kiểm tra: 2026-11-02. Kết luận: **PASS**.

- Customer Web được khởi động lại tại `http://localhost:5015`; runtime phục vụ đúng `site.css` hiện tại.
- Edge DevTools device emulation kiểm tra đúng 375, 768, 1024 và 1440 px: không tràn ngang, hero không cắt chữ, bố cục Home đáp ứng đúng.
- Ảnh logo thương hiệu lỗi được ẩn và chỉ hiện fallback; không còn chồng lớp.
- Header/navigation, CTA và footer hiển thị đúng trên mobile/desktop. Tab và Shift+Tab có thứ tự hợp lý, focus ring nhìn rõ.
- `prefers-reduced-motion: reduce` được nhận diện; transition tương tác được rút về gần 0.
- Ảnh chụp bằng chứng: `docs/artifacts/S29.9B.2/`.

Lưu ý: cửa sổ Edge CLI thông thường có chiều rộng nội dung tối thiểu 496 px; kết quả cuối dùng CDP device emulation để kiểm tra chính xác 375 px.
