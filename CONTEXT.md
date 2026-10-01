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
