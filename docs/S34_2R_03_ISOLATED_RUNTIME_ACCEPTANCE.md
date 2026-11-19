# S34.2R-03 — Nghiệm thu runtime .NET 10 cách ly

Ngày 19/11/2026. **BLOCKED tại preflight do RAM host.** Giữ nguyên runtime và dữ liệu hiện có.

## A. Baseline Integrity — PASS

`main` ở `4d77bd0a3ab619fae70d17a2f750d74233082e9e`. Working tree còn diff .NET 10 của R-02 và tài liệu R-01/R-02 như dự kiến. Hai image `linux/amd64` khớp bằng chứng R-02.

| Tag | Image ID |
| --- | --- |
| `phonestore-api:s34-2r02-net10` | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` |
| `phonestore-web:s34-2r02-net10` | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` |

Docker Engine 29.9.2, 12 CPU logic, 7,639 GiB bộ nhớ cấp cho Docker VM. SQL đang dùng 1,443 GiB, bốn container Web/API khoảng 509 MiB cộng lại. Còn 313,66 GiB đĩa C. Ba mẫu RAM trống của Windows là **0,879 / 0,908 / 0,909 GiB**. Docker VM được cấp bộ nhớ lớn hơn RAM trống host tại lúc đo. SQL Server Linux cần tối thiểu 2 GB để khởi động, chưa gồm phần tài nguyên cần cho Web/API và Windows. [Yêu cầu SQL Server trên Linux](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/setup?view=sql-server-ver16#system-requirements)

## B. Isolated Runtime Configuration — đề xuất, chưa tạo

| Thành phần | Cấu hình dự kiến |
| --- | --- |
| Web / API | Hai image đúng ID ở trên. Bind loopback `127.0.0.1:5385` và `127.0.0.1:5386`. Web gọi API bằng DNS trên network test. |
| SQL | SQL Server 2022 instance riêng, volume mới `phonestore-s342r03-sql-data`. DB mới `PhoneStoreDB_Net10Acceptance_S342R03`, tương thích 160. Chỉ publish SQL loopback `127.0.0.1:14336` để WinForms kiểm thử. |
| Network | Network riêng `phonestore-s342r03`, không nối các container test vào network business/acceptance. |
| Uploads/credential | Chỉ dùng fixture tổng hợp, uploads riêng và read-only cho Web. Credential mới local, tài khoản runtime quyền giới hạn cho DB test. Secret nằm ngoài Git và báo cáo. |

Cổng 5385/5386/14336 chưa có listener tại lúc kiểm tra. Không có network/volume R-03 trong inventory. Trước khi tạo DB, xác nhận identity của SQL instance mới và kiểm tra tên đích trong cấu hình chính xác. Kiểm tra `DB_NAME()` sau tạo và trước khi áp schema hoặc thêm fixture. Cả hai tên DB hiện hành đều nằm ngoài phạm vi. Giữ dữ liệu test để đối chiếu, cleanup cần quyết định riêng.

## C–G. Trạng thái kiểm thử

| Hạng mục | R-03 | Bằng chứng và phần cần chạy khi đủ tài nguyên |
| --- | --- | --- |
| C. API smoke | **BLOCKED** | Chưa start image. Kiểm tra health, live, ready, Catalog v1 và OpenAPI, so JSON/status với contract .NET 8 trên fixture tương đương. |
| D. Web smoke | **BLOCKED** | Chưa start image. Kiểm tra Home/Products/Login, cookie, route protected và CT02 qua account switch/logout, không tạo đơn. |
| E. SQL Server compatibility | **BLOCKED** | Chưa kiểm tra EF Core 10 với SQL test. Xác minh schema, pagination, detail, authentication và biên UTC+7 với fixture. |
| F. WinForms runtime | **BLOCKED** | Chưa chạy kết nối test SQL. R-02 trước đó mở/đóng LoginForm offline thành công. Admin auth, đọc catalog/report và session authorization cần nghiệm thu ở target dự kiến. |
| G. Regression | **NOT RUN** | R-02 đã có targeted 129/129 và full 263/263 trên .NET 10. Dừng trước chạy lại vì RAM thấp. |

## H. Container/Data Integrity — PASS

Năm container đang chạy đều healthy, giữ ID, image ID và StartedAt từ R-02. SQL `PhoneStoreDB` và `PhoneStoreDB_AcceptanceTest` đều ONLINE, count/checksum **8/8 bảng mỗi DB** khớp R-02, blocking 0 tại lúc đo. Business có 40 sản phẩm, 15 đơn. Acceptance có 5 sản phẩm, 4 đơn, 4 chi tiết và 2 ảnh. Phiên này chỉ dùng SELECT trên các DB đó. Chưa chạy image mới hay tạo container, network, volume, database.

## I–J. Rollout Readiness & Remaining Risks

**NO-GO cho rollout.** Đối chiếu lại RAM và sức khỏe stack sau khi giải phóng tài nguyên. Cần đủ bộ nhớ chạy song song SQL test, Web/API và WinForms, rồi xác minh RAM lần nữa khi SQL test sẵn sàng trước khi bật các host test. Các rủi ro runtime đang chờ gồm SQL provider/TLS, query translation, session khi thay process và WinForms trên SQL Server thật. Không dùng SQL business/acceptance làm phương án thay thế trong phiên này.

## K. Suggested Commit Message

Khi R-03 hoàn thành, đề xuất `test(runtime): record isolated .NET 10 acceptance`. Chưa stage, commit, push hoặc deploy.
