# PhoneStore Docker Compose & CI

Compose đóng gói ba dịch vụ Linux `web`, `api`, `db`; Admin WinForms tiếp tục chạy trực tiếp trên Windows. Cấu hình không tự migrate, seed hoặc restore database.

## 1. Chuẩn bị an toàn

Yêu cầu Docker Desktop (Linux containers), Docker Compose và đủ tài nguyên cho SQL Server 2022. Sao chép cấu hình mẫu:

```powershell
Copy-Item .env.example .env
```

Trong `.env`, bắt buộc:

- tự đặt `MSSQL_SA_PASSWORD` mạnh, duy nhất; không dùng giá trị ví dụ hoặc commit file này;
- đặt `PRODUCT_UPLOADS_PATH` thành đường dẫn tuyệt đối tới thư mục ảnh runtime mà Admin Windows cũng quản lý;
- chọn `SQL_PORT` còn trống (mặc định đề xuất 14334, vì 14333 có thể thuộc stack cũ);
- giữ `COMPOSE_PROJECT_NAME` riêng để không tác động stack/volume cũ.

`.env` đã được `.gitignore` loại trừ. Tuy vậy, người có quyền Docker vẫn có thể xem environment của container; production cần secret manager phù hợp. Credential Docker cũ từng xuất hiện trong Git history, nên phải được đánh giá/rotate trước khi công khai repository; tài liệu này không hướng dẫn rewrite history.

Kiểm cấu hình mà không khởi động container:

```powershell
docker compose config --quiet
```

Lệnh phải dừng ngay nếu thiếu password hoặc đường dẫn uploads.

## 2. Kiến trúc Compose

| Service | Host mặc định | Container | Vai trò |
|---|---:|---:|---|
| `web` | `127.0.0.1:5015` | `8080` | Customer Web; gọi `http://api:8080/` khi feature flag bật |
| `api` | `127.0.0.1:5025` | `8080` | REST API read-only; health tại `/api/health` |
| `db` | `127.0.0.1:14334` | `1433` | SQL Server 2022, dữ liệu ở named volume riêng |

Web và API dùng service hostname `db`, không dùng `localhost`. `CatalogApi__BaseUrl` trong Web dùng service hostname `api`. Web/API chỉ khởi động sau khi dependencies healthy.

Ảnh runtime không nằm trong image hoặc Git. Compose bind-mount `PRODUCT_UPLOADS_PATH` read-only vào `/app/wwwroot/uploads/products`; Admin WinForms cần được cấu hình riêng để ghi vào cùng host directory. Không xóa orphan hoặc đổi tên ảnh khi chưa đối chiếu database.

## 3. Build và chạy có kiểm soát

S31.5B chỉ validate/build, **không chạy** các lệnh dưới đây. Khi đã có phê duyệt deployment và đã kiểm tra ports/project name:

```powershell
docker compose build
docker compose up -d
docker compose ps
```

Truy cập Web tại `http://localhost:5015`, API health tại `http://localhost:5025/api/health`. Xem log theo service, không in `.env`:

```powershell
docker compose logs -f web
docker compose logs -f api
docker compose logs -f db
```

Dừng stack nhưng giữ named volume:

```powershell
docker compose down
```

Không dùng `down -v`, không xóa volume và không restore/migrate/seed nếu chưa được phê duyệt riêng. Database nguồn có dữ liệu cá nhân; backup phải nằm ngoài Git và cần quy trình S31.5C.

## 4. Admin WinForms

S31.5B không đổi cấu hình Admin. Ở bước nghiệm thu sau, Admin Windows có thể dùng cấu hình tạm trỏ tới `localhost,<SQL_PORT>` và cùng `PRODUCT_UPLOADS_PATH`; không ghi đè cấu hình kết nối PhoneStoreDB chính.

## 5. CI

Workflow `.github/workflows/ci.yml`:

1. restore/build/test solution trên Windows;
2. build riêng Web image và API image trên Linux;
3. tạo credential kiểm tra tạm thời trong runner rồi chạy `docker compose config --quiet`.

CI không push image, không chạy Compose và không chứa credential dùng lại được.

## 6. Giới hạn còn lại

- Ứng dụng vẫn dùng tài khoản `sa`; least-privilege SQL login cần thay đổi database được phê duyệt riêng.
- SQL image còn dùng tag `2022-latest`; cần pin bản đã nghiệm thu trước production.
- Healthcheck HTTP dùng Bash TCP có sẵn theo giả định của base image và cần runtime acceptance ở S31.5C/D.
- Không có dữ liệu/ảnh trong Git checkout; backup/restore và asset transfer phải được nghiệm thu riêng.
