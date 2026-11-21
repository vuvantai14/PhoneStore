# S35.2B — Phát hành Web và API lên GHCR

Ngày 2026-11-21. **Kiểm tra cục bộ PASS. Nghiệm thu GHCR đang chờ lần push chứa workflow mới.**

## Baseline

Trước khi sửa, working tree sạch. HEAD của `main` và `refs/heads/main` trên origin cùng ở `5ba636e8c75df015f06d7ddc3a41745c35fa9e70`. CI trước S35.2B thành công tại revision này. [GitHub Actions run 44558140643](https://github.com/vuvantai14/PhoneStore/actions/runs/44558140643)

## Điều kiện phát hành và quyền

Job `publish-ghcr` trong `.github/workflows/ci.yml` chỉ chạy cho sự kiện push lên `main` của `vuvantai14/PhoneStore` sau khi cả hai job Windows và Ubuntu thành công. Điều kiện này bao gồm build/test và artifact WinForms trên Windows, hai Docker build và kiểm tra Compose trên Ubuntu. Pull request và workflow_dispatch chỉ thực hiện CI.

Các job CI kế thừa `contents: read`. Riêng job publish có thêm `packages: write`, đăng nhập GHCR bằng `GITHUB_TOKEN` của job và ghim các action theo commit SHA. Tất cả checkout dùng `persist-credentials: false`. Token chỉ được truyền cho bước đăng nhập registry, không được truyền vào Dockerfile dưới dạng build argument. [GitHub Container registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)

## Image và dữ liệu build

| Ứng dụng | Image | Dockerfile |
| --- | --- | --- |
| Customer Web | `ghcr.io/vuvantai14/phonestore-web` | `Dockerfile` |
| REST API | `ghcr.io/vuvantai14/phonestore-api` | `src/PhoneStore.Api/Dockerfile` |

Hai repository image dùng tên chữ thường. Tag duy nhất do workflow phát hành là `sha-<đủ 40 ký tự commit SHA>`. Buildx nhắm `linux/amd64` và gắn nhãn OCI source/revision. Job publish build lại cùng checkout, Dockerfile và platform với job Ubuntu. Base image dùng tag có thể thay đổi giữa các lần build. [Docker image references](https://docs.docker.com/reference/cli/docker/image/tag/)

Hai job dùng `scripts/ci/prepare_image_context.py` để tạo context trong thư mục tạm của runner. Script chỉ chép các đầu vào được Git theo dõi của hai ứng dụng và ba project phụ thuộc, loại uploads, file môi trường và file dữ liệu SQL/backup. Ba file appsettings hiện có trong context được loại mục `ConnectionStrings`; các khóa khác giữ nguyên giá trị. Compose cung cấp kết nối ở runtime. File cấu hình trong checkout giữ nguyên.

## Manifest cho CD và rollback

Sau hai lần push image thành công, workflow lấy output `digest` của từng build, kiểm tra định dạng SHA-256 và đọc lại descriptor của từng tag trên GHCR. Nếu một tag trỏ sai digest hoặc thiếu output, job dừng trước khi upload manifest. [Docker Buildx imagetools inspect](https://docs.docker.com/reference/cli/docker/buildx/imagetools/inspect/)

Artifact `phonestore-images-<sha>-run<run_id>.<run_attempt>` lưu `images.json` trong 30 ngày. Manifest chứa repository, commit SHA, run ID, run attempt, cùng tag đầy đủ, digest và reference `name@sha256:…` của Web/API. CD sau này cần dùng cả cặp reference từ cùng một manifest thành công. Khi rollback, chọn cặp của phiên bản đã nghiệm thu trước đó. Cần lưu manifest của các phiên bản còn phải hỗ trợ trước khi artifact hết hạn.

## Bằng chứng cục bộ

| Kiểm tra | Kết quả |
| --- | --- |
| Parse YAML, kiểm tra triggers, dependencies, điều kiện publish, permissions và khai báo build | PASS |
| Tạo context tạm, đối chiếu đầu vào COPY của hai Dockerfile và ba file appsettings với bản gốc | PASS |
| Chạy logic manifest với kết quả registry mô phỏng | PASS cả ba ca. Digest khớp tạo cặp reference, digest thiếu hoặc lệch chặn manifest. |
| `python scripts/ci/validate_business_compose.py` | PASS với cấu hình merge base, restore và net10. Docker CLI cảnh báo sandbox hạn chế đọc cấu hình cá nhân. |
| `git diff --check` | PASS |

Chưa chạy lại .NET tests hoặc build image trong S35.2B. Dockerfile, source ứng dụng và Compose không có thay đổi. Chưa có digest phát hành cho revision mới.

## Nghiệm thu trên GitHub

Review trước checkpoint ngày 2026-11-21 đã đối chiếu SHA với tag trên repository chính thức và ghim thêm sáu tham chiếu action của hai job CI. Toàn bộ action trong workflow hiện dùng SHA đầy đủ. Kiểm tra cục bộ 251 file trong build context đã xác nhận đầu vào COPY, ba appsettings và các khóa cấu hình ngoài `ConnectionStrings`. Compose khai báo `ConnectionStrings__PhoneStoreConnection` cho cả Web và API. Quét theo mẫu không phát hiện credential trong context. Kiểm tra manifest đạt 4/4 ca mô phỏng, kể cả trường hợp truy vấn registry thất bại sau khi đã xác minh image Web.

1. Sau khi được duyệt push, chọn run có event `push`, branch `main` và head SHA đúng commit S35.2B. Kiểm tra cả hai job CI và job publish thành công.
2. Tải manifest từ đúng run/attempt. Đối chiếu SHA, tên hai package, hai tag và hai digest bằng `docker buildx imagetools inspect <image>:sha-<commit>`. Xác minh cả hai reference theo digest có thể đọc từ registry.
3. Kiểm tra package liên kết với repository, quyền đọc của client CD dự kiến và visibility mong muốn. Quyền package hiện hữu có thể ngăn lần publish đầu. Nếu một image đã push nhưng job còn lại hoặc upload artifact lỗi, giữ trạng thái phát hành chưa đạt cho cả cặp.

Giai đoạn này chỉ chuẩn bị phát hành image. Môi trường business, acceptance và SQL không có thao tác triển khai.
