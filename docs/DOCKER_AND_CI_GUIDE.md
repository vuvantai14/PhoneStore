# Hướng dẫn Docker Compose & GitHub Actions CI cho PhoneStore

Tài liệu này hướng dẫn cách chạy PhoneStore.Web cùng SQL Server trên Docker và cách thức hoạt động của luồng kiểm thử tự động (CI) trên GitHub Actions phục vụ báo cáo môn học.

---

## 1. Yêu cầu môi trường & Cài đặt

1. **Docker Desktop:**
   - Cài đặt [Docker Desktop for Windows](https://www.docker.com/products/docker-desktop/).
   - Trong quá trình cài đặt, chọn kích hoạt **WSL 2 backend** (Windows Subsystem for Linux).
   - Khởi động Docker Desktop và đảm bảo biểu tượng Docker ở thanh taskbar hiển thị trạng thái `Engine running`.
2. **Git & .NET 8 SDK:** Đã có sẵn trên máy phát triển.

---

## 2. Cấu hình biến môi trường `.env`

Để đảm bảo tính bảo mật (không đưa mật khẩu lên Git) và tránh xung đột cổng với các ứng dụng đang chạy trên Windows:

1. Mở PowerShell hoặc CMD tại thư mục gốc dự án `C:\PhoneStore\PhoneStore`:
   ```powershell
   Copy-Item .env.example .env
   ```
2. Mở file `.env` vừa tạo để kiểm tra các cấu hình mặc định:
   ```env
   # Cổng truy cập Web từ trình duyệt (mặc định 8080 để tránh đụng độ cổng 5015 trên máy host)
   WEB_PORT=8080

   # Cổng kết nối SQL Server từ máy host (mặc định 14333 để tránh đụng độ cổng 1433 của Windows SQL Server)
   SQL_PORT=14333

   # Mật khẩu quản trị sa của SQL Server trong container
   MSSQL_SA_PASSWORD=PhoneStore@2026!

   # Tên CSDL riêng trong Docker (hoàn toàn cách ly với PhoneStoreDB chính trên Windows)
   DB_NAME=PhoneStoreDockerDB

   # Môi trường chạy
   ASPNETCORE_ENVIRONMENT=Development
   ```
   > [!NOTE]
   > File `.env` đã được liệt kê trong `.gitignore` nên tuyệt đối an toàn, không bị commit lên GitHub.

---

## 3. Khởi chạy ứng dụng bằng Docker Compose

### A. Lệnh khởi chạy
Tại thư mục gốc `C:\PhoneStore\PhoneStore`, thực thi lệnh:
```bash
docker compose up -d --build
```
- `--build`: Build lại Dockerfile của PhoneStore.Web theo mã nguồn mới nhất.
- `-d`: Chạy ngầm trong nền (detached mode).

### B. Kiểm tra trạng thái các container
```bash
docker compose ps
```
Kết quả hiển thị tương tự:
```
NAME              IMAGE                                     COMMAND                  SERVICE   CREATED          STATUS                    PORTS
phonestore-db     mcr.microsoft.com/mssql/server:2022-latest "/opt/mssql/bin/sqls…"   db        30 seconds ago   Up 30 seconds (healthy)   0.0.0.0:14333->1433/tcp
phonestore-web    phonestore-web                           "dotnet PhoneStore.W…"   web       10 seconds ago   Up 10 seconds             0.0.0.0:8080->8080/tcp
```
> [!IMPORTANT]
> Dịch vụ `web` được cấu hình `depends_on: { db: { condition: service_healthy } }`. Do đó, `web` sẽ tự động chờ dịch vụ `db` khởi động hoàn tất và phản hồi truy vấn `SELECT 1` thành công trước khi bắt đầu phục vụ.

### C. Xem nhật ký (Logs)
- Xem log tổng hợp:
  ```bash
  docker compose logs -f
  ```
- Xem log riêng của Web:
  ```bash
  docker compose logs -f web
  ```
- Xem log riêng của Database:
  ```bash
  docker compose logs -f db
  ```
*(Nhấn `Ctrl + C` để thoát màn hình xem log).*

---

## 4. Truy cập ứng dụng & Khởi tạo dữ liệu Docker

### A. Truy cập Web
Mở trình duyệt truy cập:
```
http://localhost:8080
```
*(Nếu bạn đổi `WEB_PORT` trong `.env` thành cổng khác, hãy truy cập theo cổng đó).*

### B. Khởi tạo Database cho môi trường Docker (Nếu cần)
Vì Docker sử dụng CSDL cách ly (`PhoneStoreDockerDB`) và không tự ý migrate tự động, khi cần chạy migration lần đầu trên môi trường Docker, bạn có thể thực thi từ máy host:
```bash
dotnet ef database update --project src/PhoneStore.Data --startup-project src/PhoneStore.Web --connection "Server=localhost,14333;Database=PhoneStoreDockerDB;User Id=sa;Password=PhoneStore@2026!;TrustServerCertificate=True;MultipleActiveResultSets=true;"
```
*(Dữ liệu SQL Server trong Docker được lưu bền vững qua Docker volume `phonestore_mssql_data`, không bị mất khi restart container).*

---

## 5. Dừng và dọn dẹp Container

- **Dừng các container (giữ nguyên dữ liệu database):**
  ```bash
  docker compose down
  ```
- **Dừng và xóa toàn bộ dữ liệu database volume (nếu muốn làm sạch hoàn toàn):**
  ```bash
  docker compose down -v
  ```

---

## 6. GitHub Actions CI (Tích hợp liên tục)

Quy trình CI được cấu hình tại `.github/workflows/ci.yml` và tự động kích hoạt khi có `push` hoặc `pull_request` vào nhánh `main`, hoặc chạy thủ công qua `workflow_dispatch`.

Quy trình gồm 2 Jobs chạy song song:
1. **Job 1: `build-and-test-windows` (Runner: `windows-latest`):**
   - Tải source code và cấu hình .NET 8 SDK.
   - Chạy `dotnet restore PhoneStore.sln` kèm caching NuGet packages.
   - Build toàn bộ Solution Release: `dotnet build PhoneStore.sln` (bao gồm cả WinForms Admin trên môi trường Windows).
   - Chạy toàn bộ 162+ ca kiểm thử đơn vị và cách ly SQLite in-memory: `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj`.
2. **Job 2: `build-docker-ubuntu` (Runner: `ubuntu-latest`):**
   - Thiết lập Docker Buildx trên môi trường Linux.
   - Thực thi build Dockerfile đa tầng của `PhoneStore.Web` để kiểm tra khả năng đóng gói container độc lập mà không cần đẩy (push) lên Docker Hub.

### Cách kiểm tra trạng thái CI trên GitHub:
1. Đẩy code lên GitHub repository của bạn.
2. Vào tab **Actions** trên giao diện GitHub.
3. Chọn workflow **PhoneStore CI** mới nhất:
   - Cả 2 job `Build Solution & Test (Windows)` và `Build Docker Image (Ubuntu)` đều hiển thị biểu tượng **dấu tick xanh lá** (Success).

---

## 7. Mẫu trình bày minh chứng cho Giảng viên

Khi báo cáo hoặc chụp ảnh minh chứng cho đồ án môn học, bạn chuẩn bị 3 ảnh chụp màn hình sau:

1. **Minh chứng 1 — Docker Containers đang chạy:**
   - Mở PowerShell / CMD chạy lệnh:
     ```bash
     docker compose ps
     ```
   - Chụp màn hình thấy rõ 2 container `phonestore-db` (healthy) và `phonestore-web` (Up) cùng các cổng `8080` và `14333`.
   - Hoặc chụp giao diện ứng dụng **Docker Desktop**, tab *Containers*, hiển thị nhóm `phonestore` màu xanh lá đang chạy.
2. **Minh chứng 2 — Giao diện Web chạy từ Docker:**
   - Mở trình duyệt Chrome/Edge truy cập `http://localhost:8080`.
   - Chụp toàn màn hình gồm cả thanh địa chỉ URL `localhost:8080` và giao diện trang chủ / danh sách sản phẩm của PhoneStore.
3. **Minh chứng 3 — GitHub Actions CI báo xanh:**
   - Mở trình duyệt vào trang GitHub repository -> Tab **Actions**.
   - Chụp màn hình workflow run có trạng thái **Success** kèm 2 jobs màu xanh:
     - `Build Solution & Test (Windows)`
     - `Build Docker Image (Ubuntu)`
