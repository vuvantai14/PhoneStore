# S35.3C — Readiness cho diễn tập Docker cách ly

**PASS cho kiểm tra tĩnh về cách ly. BLOCKED cho diễn tập đầy đủ với SQL Server.** Đo ngày 2026-11-21. HEAD `main` và ref theo dõi `origin/main` là `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`. Phạm vi làm việc đã có hai báo cáo S35.3A/B, năm file tooling của S35.3B và thay đổi tiến độ. Audit giữ nguyên tooling và bổ sung báo cáo cùng dòng tiến độ. [Thiết kế CD](S35_3A_WINDOWS_CD_DESIGN.md) · [Bằng chứng S35.3B](S35_3B_CD_TOOLING_REHEARSAL.md)

## A. Tài nguyên hiện tại

| Hạng mục | Bằng chứng |
| --- | --- |
| Windows | RAM 15,77 GiB. Ba mẫu gần nhau còn 4,57 / 4,57 / 4,58 GiB. CPU i5-11400H, 6 lõi / 12 luồng, tải snapshot 29%. Page file dùng 1.020 / 10.602 MiB. |
| Docker Desktop và WSL2 | Engine 29.9.2, Compose v5.6.1, Linux WSL2. Docker thấy 12 CPU và 7,64 GiB RAM hiệu lực. WSL có 5.845–5.852 MiB `MemAvailable`, swap 2.048 MiB và chưa dùng trong ba mẫu. Không thấy file `.wslconfig` của tài khoản hiện tại. |
| Đĩa | Windows C: còn 316,4 GiB. Filesystem ảo Docker báo 937,8 GiB khả dụng. Dung lượng thật khi image và VHDX tăng vẫn phụ thuộc ổ Windows. |
| Business runtime | `web` healthy, 131,3 MiB và 0,02% CPU. `api` healthy, 92,99 MiB và 0,49% CPU. `db` healthy, 1,184 GiB và 1,58% CPU. Đây là ảnh chụp tại một thời điểm. |

RAM Windows và WSL là hai số đo của cùng host. Dùng cả hai để đánh giá áp lực cấp phát. [WSL configuration](https://learn.microsoft.com/en-us/windows/wsl/wsl-config)

## B. Cách ly

Static model được render với giá trị tổng hợp và chỉ xuất kết luận. Project là `phonestore-cd-rehearsal`. Tên container do Compose tạo, hiện không có xung đột tên dự kiến. Network nội bộ `phonestore-cd-rehearsal_isolated` và volume cục bộ `phonestore-cd-rehearsal_sql` chưa tồn tại. Hai app chỉ dùng network này, không có mount. Windows không có listener trên `5385` hoặc `5386` tại thời điểm audit. Hai port này cũng nằm ngoài các dải IPv4 bị loại trừ đã kiểm tra.

SQL rehearsal không publish port. Chỉ service `db` gắn volume SQL riêng ở `/var/opt/mssql`. Hai app trỏ `db,1433`, database `PhoneStoreDB_CdRehearsal`, login `cd_rehearsal`. Cấu hình dùng credential riêng qua môi trường tác vụ. Khi chạy thật, xác minh lại nhãn project, volume, mount, thành viên network, port và database đích trước mọi thao tác.

SQL business vẫn healthy với ID `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6`, StartedAt `2026-11-21T00:27:43.48526107Z`, restart count 0 và volume `phonestore-restore-c2_mssql_data`. ID và StartedAt khớp S35.3A. Guard và lệnh Compose của tooling dùng project tách biệt và chỉ chọn `api` hoặc `web`.

## C. Hai mức diễn tập

| Mức | Kiểm tra được | Phần còn lại |
| --- | --- | --- |
| Test doubles PowerShell | Luồng quyết định, kiểm tra manifest/digest, thứ tự API rồi Web, recovery/rollback và lỗi SQL invariant. Đã chạy lại và PASS. | Docker Engine, mount thật, network và ứng dụng thật. |
| Docker với HTTP mock readiness và container giữ chỗ | Có thể kiểm tra Docker thực sự thay đúng service, giữ container giữ chỗ và volume, quan sát HTTP 200/503, health và phục hồi image. Dùng Compose fixture riêng có image nhẹ và giới hạn tài nguyên. | Startup Web/API của PhoneStore, kết nối và schema SQL, catalog thật. |
| Docker với image PhoneStore và SQL Server riêng | Kiểm tra pull theo digest, startup/readiness của API với SQL thật, catalog Web và rollback trên cặp image thật. | Cần baseline app cũ có readiness tương thích và database fixture riêng đã được provision. |

Bộ mock cần fixture Docker riêng được duyệt. Tooling hiện tại đòi topology đã được tạo và kiểm tra config-file label, SQL volume và cấu hình app. Vì vậy không dùng một file override mock tùy ý với entry point hiện tại. Trước diễn tập cần xác minh đường dẫn tuyệt đối trên nhãn Compose Windows sau khi chuẩn hóa dấu phân cách. So sánh chuỗi hiện tại dùng đường dẫn script có dấu `/`, còn nhãn runtime business đã quan sát dùng `\`.

## D. Gate tài nguyên và rủi ro

Máy đủ dư địa để **chuẩn bị** phương án Docker mock nhẹ. Ngân sách đề xuất cho ba container mock là tổng hard cap tối đa 384 MiB và tổng CPU khoảng 1 lõi. Cần kiểm tra lại dung lượng, listeners, bảo toàn baseline business và RAM ngay trước khi chạy. Các giới hạn chỉ áp dụng cho fixture mock.

Full rehearsal hiện **BLOCKED**. Template chưa đặt giới hạn CPU/RAM, chưa có stack diễn tập, cặp baseline rollback đã chạy healthy hay DB fixture. Ngân sách kế hoạch cho SQL và hai app là khoảng 3,5 GiB khi cold-start, với tối thiểu 1,5 GiB dự phòng cho Windows/WSL. Đây là ngưỡng vận hành đề xuất cho host chung, cần kiểm chứng trên fixture. Ba mẫu Windows dưới mức 5 GiB theo ngân sách đó. Filesystem ảo và RAM trống tại một thời điểm không bảo đảm business tránh được paging hoặc OOM khi SQL khởi động. [Docker resource constraints](https://docs.docker.com/engine/containers/resource_constraints/) · [SQL Server on Linux](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/setup?view=sql-server-ver17)

Khi thiếu tài nguyên, hoãn trước bước pull hoặc recreate. Nếu đã chạy, ưu tiên hoàn tất rollback hoặc xác minh journal theo quy trình, sau đó dừng có kiểm soát riêng các container rehearsal đã đối chiếu project và ID. Docker pause giữ bộ nhớ tiến trình nên không thích hợp để giải phóng RAM. Nếu phải dừng khi giao dịch đang dang dở, lưu nhật ký và đối chiếu topology trước khi chạy `-Recover`. Business và Docker Desktop tiếp tục phục vụ trong toàn bộ quy trình.

## E. Kế hoạch tám tình huống

| Tình huống | Bằng chứng cần thu trong lần Docker thật |
| --- | --- |
| Deploy thành công | ID API đổi, readiness PASS trước khi ID Web đổi. Cả hai image đích và SQL snapshot khớp. |
| API readiness lỗi | API fixture trả 503. Web giữ ID ban đầu và API trở lại image cũ healthy. |
| Web readiness lỗi | API mới đã healthy. Web fixture trả 503. Hai service trở lại image cũ và healthy. |
| Rollback khi API đã cập nhật | Mô phỏng lỗi thao tác Web sau API. So sánh journal, image IDs và probe sau rollback. |
| Gián đoạn giữa chừng | Ngắt tiến trình điều phối sau khi ghi journal API. Apply tiếp bị chặn. Recovery đọc lại journal và khôi phục cặp baseline. |
| SQL invariant mismatch | Tiêm mismatch qua test double hoặc container giữ chỗ của fixture mock, giữ kiểm tra business ở chế độ đọc. Xác minh thiếu bằng chứng SQL thì journal không báo rollback hoàn tất. |
| Manifest không hợp lệ | Sai run/attempt hoặc schema phải bị từ chối trước pull và recreation. |
| Digest không khớp | Descriptor mock lệch phải chặn release. Với registry thật, xác minh digest được phê duyệt trong dry-run. |

## F. Kết quả

Compose render và các asserts về project, service, mạng, port, volume, connection target PASS. PowerShell parser PASS 4 file. `Test-Rehearsal.ps1` PASS 10 ca engine cùng nhóm manifest/registry. `git diff --check` PASS. Kiểm tra Git, Docker, Windows và WSL dùng lệnh đọc. Không cần chạy bộ test .NET trong phiên readiness.

## G. Các điều kiện còn thiếu

1. Chuẩn bị fixture Docker mock có giới hạn tài nguyên và readiness điều khiển được. Kiểm tra guard đường dẫn Compose trên Windows và chạy tám kịch bản với snapshot business trước/sau.
2. Trước full rehearsal, xác minh headroom đạt ngân sách, giới hạn CPU/RAM cục bộ và một DB fixture tổng hợp đã được provision riêng. Dùng hai image rollback đã được xác minh và kiểm tra quyền Docker cùng quyền đọc GHCR từ tài khoản tác vụ. Migrations và thay đổi SQL nằm ngoài phiên readiness này.

## H. Quyết định

**GO cho bước chuẩn bị fixture Docker mock nhẹ. NO-GO cho Docker rehearsal đầy đủ tại thời điểm audit.** Readiness kết thúc tại đây với bằng chứng test doubles, Compose tĩnh, tài nguyên và trạng thái SQL business.
