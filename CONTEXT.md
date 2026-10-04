# PhoneStore Project Context

## 1. Project

Topic:
Xây dựng hệ thống quản lý và bán điện thoại di động.

Technology:
- C#
- ASP.NET Core MVC: Customer Web
- Windows Forms: Admin Desktop
- Entity Framework Core
- Microsoft SQL Server

Web và Admin dùng chung dữ liệu nghiệp vụ.

## 2. Architecture

Projects:
- PhoneStore.Models
- PhoneStore.Data
- PhoneStore.Business
- PhoneStore.Web
- PhoneStore.Admin

Dependency:

Models
↑
Data
↑
Business
↑
├── Web
└── Admin

Rules:
- Không circular dependency.
- Business logic dùng chung đặt trong Business layer.
- Data access đặt trong Data layer.
- Không hard-code dữ liệu nghiệp vụ.
- Không tự thêm chức năng ngoài scope.

## 3. Actors

Customer:
- Trang chủ
- Xem sản phẩm
- Tìm kiếm
- Lọc
- Chi tiết sản phẩm
- Đăng ký
- Đăng nhập
- Tài khoản
- Giỏ hàng
- Đặt hàng
- Xem đơn hàng
- Đăng xuất

Admin:
- Đăng nhập
- Dashboard
- Quản lý điện thoại
- Quản lý thương hiệu
- Quản lý danh mục
- Quản lý khách hàng
- Quản lý đơn hàng
- Báo cáo/thống kê
- Đăng xuất

## 4. Database

Database:
PhoneStoreDB

Business tables:
1. Roles
2. Users
3. Brands
4. Categories
5. Products
6. ProductImages
7. Orders
8. OrderDetails

Relations:

Roles 1-N Users
Users 1-N Orders
Brands 1-N Products
Categories 1-N Products
Products 1-N ProductImages
Orders 1-N OrderDetails
Products 1-N OrderDetails

Do not add tables unless explicitly required by a later approved step.

Local configuration:
- Connection string key: `ConnectionStrings:PhoneStoreConnection`.
- Configure a verified SQL Server instance and database `PhoneStoreDB` in
  `src/PhoneStore.Web/appsettings.Development.json` (no secrets), or use
  environment variable `ConnectionStrings__PhoneStoreConnection` for Web and tooling.
- Development targets `taiiiii\SERVER`, database `PhoneStoreDB`, using Windows Authentication.
- Restore tooling: `dotnet tool restore`.
- Apply migration from repository root:
  `dotnet ef database update --project src/PhoneStore.Data --startup-project src/PhoneStore.Data`.
- Inspect database tables and Roles after applying; do not assume database success from build.

## 5. Business Rules

Product:
- Price > 0
- StockQuantity >= 0
- Product belongs to Brand.
- Product belongs to Category.
- Prefer deactivation instead of physical deletion when historical
  business data exists.

Order statuses:

Pending
→ Confirmed
→ Shipping
→ Completed

Alternative:

Pending
→ Cancelled

OrderDetail:
- Quantity > 0
- UnitPrice stores purchase-time price.
- Old order price must not change when Product.Price changes.

Order total:

TotalAmount =
SUM(OrderDetail.Quantity * OrderDetail.UnitPrice)

Checkout later must be transactional.

## 6. Web Screens

W01 Home
W02 Product List
W03 Product Detail
W04 Cart
W05 Login
W06 Register
W07 Checkout
W08 Order Success
W09 Account
W10 My Orders
W11 Order Detail

## 7. Admin Screens

A01 Login
A02 Dashboard
A03 Product Management
A04 Product Add/Edit
A05 Brand Management
A06 Category Management
A07 Customer Management
A08 Order Management
A09 Order Detail
A10 Reports

## 8. UI/UX Direction

IMPORTANT:
This section is for later UI steps.
Do NOT implement UI during S8.

Design direction:
- Modern phone/e-commerce store
- Clean
- Professional
- White/light background
- Blue primary accent
- Clear visual hierarchy
- Good whitespace
- Consistent typography
- Consistent spacing
- Consistent buttons/cards/forms
- Responsive Web

Customer Web:
- Modern e-commerce appearance
- Responsive desktop/tablet/mobile
- Reusable components/partials
- Clear product cards
- Clear search/filter UX
- Proper form states
- Avoid default Bootstrap appearance

Admin:
- Professional desktop management interface
- Sidebar
- Header/top area
- Main content
- Clear DataGridView/table
- Search/filter/action controls
- Consistent Add/Edit forms

Reference images provided in future UI tasks are DESIGN REFERENCES only.

May copy:
- layout
- hierarchy
- spacing
- proportions
- design language

Must NOT copy:
- fake products
- fake prices
- fake customers
- fake orders
- fake statistics
- unsupported features

Real application data must come from the database.

For UI tasks:
- inspect available UI/UX/frontend skills first
- use appropriate skills when available
- inspect reference images before implementation
- do not redesign unrelated screens

## 9. Coding Rules

Before every future task:

1. Read CONTEXT.md first.
2. Inspect only files relevant to the current step.
3. Preserve working code.
4. Reuse existing implementation where appropriate.
5. Do not add features outside the requested step.
6. Do not add fake business data.
7. Do not add unnecessary packages.
8. Do not silently change database design.
9. Build/test affected projects after changes.
10. Fix errors caused by the current step.
11. Stop after the requested step.

## 10. Current Progress

Completed:
- S1 Scope
- S2 Functional Analysis
- S3 Use Cases / Business Flows
- S4 Screen Design / Mapping
- S5 Database Design
- S6 Architecture Design
- S7 Solution Initialization
- S8 Data Foundation
- S9 Customer Web UI Foundation + Home + Products
- S10 Product Detail + Product Browsing Polish
- S11 Customer Authentication
- S11.5 Customer UI/UX Final Polish
- S12 Cart + Checkout + Order Creation
- S13 WinForms Admin Login + Admin UI Foundation
- S14 Admin Brands + Categories + Products + Product Images

S8 completed:
- Entities created.
- EF Core 8.0.28 configured.
- PhoneStoreDbContext configured.
- InitialCreate migration created and inspected.
- PhoneStoreDB created on `taiiiii\SERVER` using Windows Authentication.
- Database update verified: 8 business tables and `__EFMigrationsHistory` present;
  only Admin/Customer roles seeded; all 7 other business tables empty.
- Build verified: 0 warnings, 0 errors.

S9 completed:
- Customer Web UI foundation and shared layout.
- Home with newest active products, database brand/category navigation and empty states.
- Product List at `/products`; reusable product card with local image fallback.
- Server-side search/filter/sort through Web → Business → Data.
- Responsive foundation verified at 1440/820/390/320 px without horizontal overflow.
- Build verified: 0 warnings, 0 errors; Home and /products return HTTP 200.
- No fake business data or schema/migration changes.
- Catalog is empty: validation/query execution verified; populated-result behavior remains unverified.

S10 completed:
- Product Detail at `/products/{id}` through existing read service; active products only.
- Product image gallery with keyboard-accessible thumbnail buttons and local fallback.
- Specifications omit empty values; description is rendered as safely encoded text.
- Related products: up to 4 active products in the same brand/category, excluding current product.
- Shared Product Card → Detail navigation.
- Responsive Product Detail; clean 404 verified at 1440/820/390/320 px.
- Build verified: 0 warnings, 0 errors; S9 browser regression checks passed.
- No fake business data or migration/schema changes.
- Product Detail runtime test pending real product data (including gallery and inactive-product verification).

S11 completed:
- Customer registration with normalized email, duplicate-email handling and database Customer role assignment.
- Customer login/logout; secure PasswordHasher<User> hashing; no Identity tables.
- Cookie authentication with UserId/FullName/Email/Role claims, RememberMe and local-only ReturnUrl.
- Anti-forgery on authentication POSTs; inactive login blocked and inactive sessions revoked.
- Authentication-aware header, responsive Login/Register and Customer authorization policy.
- Runtime/security checks passed; temporary accounts created through Register and removed afterward.
- Build verified: 0 warnings, 0 errors; S9/S10 browsing regression checks passed.
- No migration/schema changes. Product Detail still awaits real product data for populated runtime checks.

S11.5 completed:
- Customer UI/UX final polish; existing white/blue/navy visual identity preserved.
- Header/footer proportions refined; desktop header remains on one row where space allows.
- Home and Products spacing refined with shared spacing/control tokens.
- Product and existing Cart empty states refined without fake business data.
- Login/Register refined with consistent card width, spacing, focus and touch targets.
- Customer responsive consistency verified at 1440/1024/1000/820/701/700/390/320 px.
- Home, Products, Cart, Login/Register and Product Detail 404 verified without horizontal overflow.
- Search/filter/sort regression checks and invalid authentication forms passed; backend/authentication code unchanged.
- Build verified: 0 warnings, 0 errors; no data inserted, schema changes or migrations.
- Populated Product Detail/gallery/related products and authenticated-session runtime checks await real data/accounts.
- No additional S12 functionality implemented; existing S12 implementation preserved.

S12 completed:
- Session cart stores ProductId/Quantity; add/update/remove use POST with anti-forgery.
- Cart indicator reads Session; cart totals use current database prices.
- Customer-only checkout with safe Login ReturnUrl, shipping prefill and validation.
- Transactional Order/OrderDetails creation with server-side prices, UnitPrice snapshot and Pending status.
- Stock revalidated inside a Serializable transaction and reduced only with successful order commit.
- Cart clears after commit; PRG and a per-submission in-memory guard prevent duplicate submissions on this Web instance.
- Order Success enforces authenticated order ownership; responsive Cart/Checkout/Success views.
- Empty-state/security/browser regression checks passed; temporary Register accounts removed.
- Build verified: 0 warnings, 0 errors; schema unchanged and no migration added.
- Full purchase-flow runtime test pending real product data (including success refresh and cross-customer order access).
- Session cart and submission guard currently use in-process memory.

S13 completed:
- WinForms Admin authentication through Business → existing AuthUserStore/Data → PhoneStoreDB.
- Shared PasswordHasher<User>; active Admin role required, Customer/inactive/invalid credentials blocked.
- Safe in-memory Admin session with identity only; logout disposes shell, clears session and returns to a fresh LoginForm.
- Admin Login UI with Vietnamese validation, masked password, show/hide, Enter submit and safe connection/error messages.
- Main Admin shell, persistent sidebar, current-page/account header, Dashboard foundation and reusable module placeholder.
- Dock/TableLayoutPanel desktop foundation with resizing and DPI scaling; screenshots verified at 120 DPI.
- Exact startup/configuration/DI and invalid login against SQL verified; isolated authentication/UI/lifecycle checks passed without database writes.
- Admin login runtime test pending real Admin account.
- Real Customer/inactive login runtime checks also await existing accounts; role/inactive rejection verified in isolated service tests.
- Customer Web preserved; browsing/search/filter/sort, Detail 404, anonymous checkout protection and invalid auth forms regression checks passed.
- Restore/build verified: 0 warnings, 0 errors; no schema/migration changes or fake data added.

S14 completed:
- Brand/Category management: database list/search, Add/Edit dialogs, activate/deactivate, trimmed names and safe unique-constraint validation.
- Product management: database search and brand/category/status filters, Add/Edit specifications, active/historical lookup handling and business-side validation/timestamps.
- Product image management: multiple local images, transactional primary-image changes, nonnegative display order and safe association removal.
- Admin management uses Business → Data → the same PhoneStoreDB as Customer Web; authentication/session and S13 shell preserved.
- Image storage uses configured `ProductImages:UploadsDirectory`, resolved against Admin AppContext.BaseDirectory; development value `../../../../PhoneStore.Web/wwwroot/uploads/products` targets Web wwwroot.
- Deployment can override `ProductImages__UploadsDirectory` with an absolute/shared Web uploads directory accessible to Admin; database URLs remain `/uploads/products/<generated-guid>.<jpg|jpeg|png|webp>`.
- Images validate format/signature and a 10 MB limit. Only unreferenced generated files inside the managed directory are deleted; unsafe cleanup or uncertain commit outcomes retain files and may require manual orphan cleanup.
- Verification passed: 62 lookup and 69 product service assertions, 46 lookup and 44 product UI assertions at 120 DPI, 17 local storage checks and 13 write-blocked checks against the real SQL instance.
- Exact Admin startup/configuration/DI and normal `dotnet run` Login window verified without authentication bypass; Customer browsing/search/filter/sort, responsive empty states, Detail 404 and anonymous/auth-form protection regression checks passed.
- Restore/build verified: 0 warnings, 0 errors; no schema changes, migrations, fake seed or business database writes.
- Admin/Customer accounts and catalog/order tables remain empty. Full authenticated Admin CRUD and populated Web Product Detail/image integration remain runtime pending real accounts/data; shared-database integration structurally verified.
- S15 not started; customer/order management, dashboard statistics and reports remain unimplemented.
