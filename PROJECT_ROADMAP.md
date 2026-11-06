# Kimia Faravar Inventory Management System

> سند مرجع نیازمندی‌ها، تصمیم‌های معماری و نقشه راه پیاده‌سازی  
> وضعیت: مبنای اولیه مورد توافق برای توسعه MVP  
> اولویت اصلی: **سادگی عملیات > پیچیدگی غیرضروری**؛ بدون قربانی‌کردن امنیت و یکپارچگی داده

## 1. هدف و زمینه کسب‌وکار

کیمیا فرآور یک شرکت تولیدی صنعتی ایرانی با بیش از ۲۰ سال سابقه است. این سامانه برای نظارت تنبیهی بر انباردار طراحی نمی‌شود؛ هدف آن مشاهده لحظه‌ای و دقیق موجودی، سامان‌دهی سوابق، ثبت سریع ورود و خروج، جست‌وجوی ساده، گزارش‌های در دسترس و حرکت تدریجی به سمت دیجیتالی‌شدن است.

اصول تجربه کاربری:

- حداقل کلیک و حداقل ورودی اجباری
- حذف تأییدهای اضافی و تأیید مدیریتی غیرضروری برای کاربران مجاز
- پیش‌فرض‌های هوشمند، جست‌وجوی سریع و کنترل‌های بزرگ و مناسب موبایل
- حفظ زمینه کاری کاربر و پیام‌های اعتبارسنجی روشن به زبان فارسی
- جداسازی تنظیمات پیشرفته از کار روزانه انبار
- تمرکز انباردار بر سه کار: جست‌وجوی موجودی، ورود کالا و خروج کالا
- ثبت لاگ و تاریخچه برای صحت، عیب‌یابی و بازیابی، بدون غالب‌شدن بر رابط کاربری

## 2. دامنه MVP

سامانه باید یک محصول واقعی، قابل اجرا و قابل نگهداری باشد؛ نه اسکلت، ماکاپ یا صرفاً سند معماری. خروجی شامل مدل‌ها و مهاجرت‌های دیتابیس، سرویس‌های کسب‌وکار، کنترلرها، Razor Viewها، احراز هویت و مجوزها، اعتبارسنجی، داده اولیه، تست و مستندات راه‌اندازی است.

زبان رابط، پیام‌ها و توضیحات کاربر فارسی و چیدمان RTL است. شناسه‌های کد، namespaceها، کلاس‌ها، propertyها و اشیای دیتابیس انگلیسی‌اند.

خارج از دامنه این MVP:

- اپلیکیشن Native موبایل یا ویندوز
- اتصال سفارش مشتری (رزرو فعلاً دستی است)
- ثبت اجباری تأمین‌کننده برای ورود یا گیرنده برای خروج
- معماری Microservice و UI مبتنی بر Blazor، React یا Angular

## 3. فناوری و معماری

- C# و نسخه LTS پشتیبانی‌شده .NET
- ASP.NET Core MVC، Razor Views، Bootstrap 5، jQuery و AJAX در محل مناسب
- Entity Framework Core با SQL Server Provider و عملیات async
- SQL Server LocalDB برای توسعه اولیه ویندوزی
- EF Core Code First Migrations
- Built-in Dependency Injection
- Modular Monolith با مرزبندی روشن پروژه‌ها

ساختار هدف:

```text
KimiaFaravar.Inventory.sln
src/
  KimiaFaravar.Web/
  KimiaFaravar.Application/
  KimiaFaravar.Domain/
  KimiaFaravar.Infrastructure/
tests/
  KimiaFaravar.Tests/
```

مسئولیت لایه‌ها:

- `Domain`: entityها، enumها، قواعد دامنه و exceptionها
- `Application`: use caseها، قرارداد سرویس‌ها، DTOها، اعتبارسنجی و abstractionهای مجوز
- `Infrastructure`: DbContext، Fluent Configurationها، دسترسی داده، احراز هویت، migration، seed و Excel
- `Web`: MVC controller، Razor View، ViewModel، UI احراز هویت، policyها، assetها و endpointهای AJAX

قواعد معماری:

- محاسبات موجودی داخل controller قرار نمی‌گیرند.
- سرویس‌های Application به `HttpContext`، MVC، Razor، jQuery یا session مرورگر وابسته نیستند.
- MVC و API آینده از یک منطق Application مشترک استفاده می‌کنند.
- ثبت DI متمرکز است.
- تمام عملیات تغییردهنده موجودی transaction-safe هستند.
- خروجی سرویس‌ها، خطاها و pagination باید سازگار و API-ready باشند.

## 4. دیتابیس و پیکربندی

- فایل MDF توسعه زیر `App_Data` و خارج از `wwwroot` قرار می‌گیرد و در Git commit نمی‌شود.
- secretها و connection string حساس وارد source control نمی‌شوند.
- انتقال از LocalDB به SQL Server Express یا SQL Server کامل فقط با تغییر configuration ممکن است.
- روابط، PK، FK، unique index، check constraint، performance index، concurrency token و precision اعشاری با Fluent API تعریف می‌شوند.
- قیدهای یکتا حداقل برای Username، Role Code، Permission Code، SKU، ترکیب Warehouse/Product و mappingهای کاتالوگ لازم‌اند.
- مشخصات اصلی محصول relational و قابل فیلتر باقی می‌مانند؛ JSON فقط برای snapshotهای audit مجاز است.
- مقادیر quantity و conversion از `decimal` استفاده می‌کنند، نه float/double.

## 5. کاربران، نقش‌ها، مجوزها و امنیت نشست

هر کاربر دقیقاً یک نقش دارد.

مدل‌های اصلی:

- `Users`: Id, AliasName, Username, PasswordHash, RoleId, IsEnabled, CreatedAt, UpdatedAt, LastLoginAt
- `Roles`: Id, Name, Code, Description, IsEnabled
- `Permissions`: Id, Code, Name, Description
- `RolePermissions`: RoleId, PermissionId
- `UserSessions` یا سازوکار معادل برای ابطال server-side نشست
- `AuditLogs`: UserId, Action, EntityName, EntityId, OldValues, NewValues, CreatedAt

نقش‌های اولیه:

1. `CEO` — مدیرعامل؛ دسترسی کامل
2. `WarehouseManager` — مدیر انبار؛ مدیریت کاتالوگ، واحدها، تراکنش و اصلاح موجودی، گزارش‌ها و ویرایش سند ثبت‌شده با حفظ تاریخچه؛ الزاماً مدیر کاربران/نقش‌ها نیست
3. `WarehouseOperator` — کاربر انبار؛ جست‌وجو، مشاهده مجاز و ثبت ورود/خروج مطابق مجوز
4. `InternalSales` — همکاران فروش داخلی؛ مشاهده فقط‌خواندنی مطابق visibility
5. `MarketDistribution` — همکاران توزیع بازار؛ مشاهده فقط‌خواندنی مطابق visibility

مجوزهای نام‌دار اولیه:

```text
Users.Manage
Roles.Manage
Products.View
Products.Manage
ProductGroups.Manage
Specifications.Manage
Inventory.View
Inventory.ViewAll
Inventory.Receive
Inventory.Dispatch
Inventory.DispatchDirect
Inventory.DispatchApprove
Inventory.Documents.EditPosted
Inventory.Reserve
Inventory.Import
Reports.View
Reports.ViewAll
```

نگاشت نقش/مجوز قابل تغییر در دیتابیس است و controller logic نباید برای تغییر آن ویرایش شود. کاربر نمی‌تواند مجوز خودش را ارتقا دهد. کاربر غیرفعال اجازه ورود ندارد و نشست‌های فعالش ابطال می‌شوند.

امنیت الزامی:

- ASP.NET Core Identity password hashing و cookie authentication امن
- anti-forgery/CSRF برای درخواست‌های تغییردهنده MVC، validation سمت سرور و output encoding
- مجوزدهی سمت سرور و جلوگیری از IDOR؛ مخفی‌کردن دکمه مجوز محسوب نمی‌شود
- rate limit ورود، logout امن، session revocation و بررسی کاربر غیرفعال
- cookie با HttpOnly، Secure و SameSite مناسب و HTTPS در استقرار شبکه
- EF LINQ/parameterized query، error handling و logging امن
- عدم نمایش exception/اطلاعات دیتابیس و عدم ثبت password در log
- ساخت admin اولیه از configuration یا setup امن؛ بدون password تولیدی مشترک و hard-coded

## 6. کاتالوگ پویا و مشخصات محصول

### گروه‌ها و مشخصات

- `ProductGroups`: ParentId nullable، Name، Code، Description، IsEnabled، DisplayOrder
- ساختار parent-child با جلوگیری از چرخه
- `Specifications`: Name، Code، DataType، MeasurementDimension اختیاری، DefaultUnitId اختیاری، IsEnabled
- DataTypeها: Text، Decimal، Integer، Boolean، Select
- `SpecificationOptions`: SpecificationId، Value، DisplayOrder، IsEnabled
- `GroupSpecifications`: ProductGroupId، SpecificationId، IsRequired، IsVariantKey، IsFilterable، DisplayOrder

گروه فرزند مشخصات اجداد را به ارث می‌برد، بدون تکثیر فیزیکی تعریف‌ها. Application باید effective specifications را از سلسله‌مراتب حل کند، از تعریف مؤثر تکراری جلوگیری کند و تغییر مشخصات استفاده‌شده را در برابر داده محصولات موجود اعتبارسنجی کند.

### محصولات و Variantها

- `Products`: ProductGroupId، SKU، Name، Description، ProductType، BaseUnitId، IsEnabled، CreatedAt، UpdatedAt
- ProductTypeها: `RawMaterial` و `FinishedProduct`
- `ProductSpecificationValues` دارای ستون typed مناسب: TextValue، DecimalValue، IntegerValue، BooleanValue یا SpecificationOptionId

قواعد:

- SKU یکتا است و تخصیص دستی SKU همواره پشتیبانی می‌شود؛ تولید خودکار می‌تواند configurable باشد.
- فقط ترکیب‌هایی که صریحاً ثبت شده‌اند محصول می‌شوند؛ هیچ Cartesian generation خودکاری نداریم.
- مقادیر باید مطابق effective specificationهای گروه و DataType اعتبارسنجی شوند.
- تکرار variant در یک گروه بر پایه مقدار normalized مشخصات `IsVariantKey` ممنوع است.
- مشخصات عددی واحد و normalization دارند؛ مثلاً ۲۰ میلی‌متر و ۲ سانتی‌متر variant متفاوت نمی‌سازند.

## 7. واحدهای اندازه‌گیری

- `Units`: Name، Symbol، Dimension، IsEnabled
- `ProductUnits`: ProductId، UnitId، ConversionFactor، IsBaseUnit، IsEnabled
- هر محصول دقیقاً یک واحد پایه دارد و ضریب واحدهای دیگر محصول‌محور است (مثلاً کارتن یک محصول ۱۰۰ عدد و محصول دیگر ۲۴ عدد).
- تمام موجودی واقعی با واحد پایه ذخیره می‌شود.
- سازگاری dimension اعتبارسنجی می‌شود و تبدیل ناسازگار مثل kg به meter ممنوع است.
- در line سند ثبت‌شده، واحد انتخابی، مقدار ورودی و ضریب همان لحظه نگهداری می‌شوند.
- پس از وجود تراکنش، تغییر واحد پایه ممنوع است مگر فرایند تبدیل صریح و امن پیاده شود.

## 8. انبار، موجودی و هم‌زمانی

- از ابتدا جدول `Warehouses` با Name، Code و IsEnabled داریم، هرچند فعلاً یک انبار وجود دارد.
- `InventoryBalances`: WarehouseId، ProductId، Quantity، RowVersion
- قید یکتای WarehouseId + ProductId الزامی است.
- موجودی فیزیکی منفی ممنوع است.
- خروج هم‌زمان دو کاربر از آخرین موجودی باید با transaction، optimistic concurrency/atomic update و کنترل دیتابیس مدیریت شود؛ validation فرانت‌اند کافی نیست.

## 9. اسناد موجودی

### ساختار

- `InventoryDocuments`: DocumentNumber، WarehouseId، DocumentType، Status، Reason، Description، CreatedByUserId، CreatedAt، PostedAt، UpdatedAt، Version
- `InventoryDocumentItems`: InventoryDocumentId، ProductId، EnteredQuantity، UnitId، ConversionFactor، BaseQuantity، Description
- نوع‌ها: OpeningBalance، Receipt، Dispatch، StockAdjustment
- وضعیت‌ها: Draft، PendingApproval، Approved، Rejected، Posted
- فقط سند `Posted` موجودی فیزیکی را تغییر می‌دهد.
- شماره سند خوانا و معنادار است.

ورود: محصول/مقدار، شرح اختیاری، بدون الزام تأمین‌کننده.  
خروج: محصول/مقدار و reason، بدون الزام گیرنده.

دارنده `Inventory.DispatchDirect` مستقیماً post می‌کند؛ سایر کاربران مجاز درخواست تأیید می‌فرستند و سند Pending اثری بر موجودی ندارد. عملیات post کاملاً atomic است.

### ویرایش سند Posted

CEO و WarehouseManager می‌توانند در UI مثل یک ویرایش عادی عمل کنند، اما backend باید:

- نسخه و lineهای قبلی، ویرایش‌کننده و زمان ویرایش را نگه دارد.
- immutable revision یا تاریخچه audit-safe معادل ایجاد کند.
- delta موجودی را محاسبه و atomic اعمال کند.
- موجودی منفی و تعارض با رزرو فعال را رد کند.
- از overwrite هم‌زمان جلوگیری کند.
- ledger قابل توضیح و سابقه کاملاً بازیابی‌پذیر بسازد.
- هیچ balance محاسبه‌شده توسط frontend را مستقیم جایگزین نکند.

## 10. رزرو موجودی

- `InventoryReservations`: WarehouseId، ProductId، Quantity، Status، Description، CreatedByUserId، CreatedAt، UpdatedAt
- وضعیت‌ها: Active، Released، Consumed
- `AvailableQuantity = PhysicalQuantity - ActiveReservedQuantity`
- UI موجودی فیزیکی و قابل دسترس را جدا نمایش می‌دهد.
- رزرو بیش از available ممنوع است.
- ایجاد رزرو موجودی فیزیکی را کم نمی‌کند؛ release فقط available را آزاد می‌کند.
- consume رزرو و post خروج مرتبط باید سازگار و atomic باشد.

## 11. قواعد مشاهده موجودی

مخاطبان محدودشونده: `InternalSales` و `MarketDistribution`. CEO و WarehouseManager همه موجودی را می‌بینند.

مدل پیشنهادی:

- `GroupVisibilityRules`
- `ProductVisibilityRules`
- سطوح: Hidden، VisibleWithoutQuantity، VisibleWithQuantity

قواعد:

- گروه‌ها inheritance سلسله‌مراتبی دارند و override محصول مقدم است.
- در نبود rule، secure default اعمال می‌شود.
- RoleId/audience ارسالی client قابل اعتماد نیست.
- enforcement در query و Application service انجام می‌شود، نه فقط Razor.
- quantity نباید از AJAX، export، report، search، API، aggregate یا summary نشت کند.
- ممکن است خود محصول دیده شود ولی Physical/Available quantity مخفی بمانند.

## 12. موجودی اولیه و Excel

دو مسیر داریم: ثبت دستی OpeningBalance و import فایل XLSX.

Import باید template دانلودی، preview، validation مرجع محصول و مقدار، نمایش ردیف نامعتبر، مدیریت امن duplicateها و confirmation صریح داشته باشد. عملیات نهایی atomic و در قالب سند OpeningBalance است، نه overwrite مستقیم InventoryBalances. ایجاد محصول ناشناخته بدون mapping کنترل‌شده ممنوع است و duplicate opening balance تصادفی باید جلوگیری شود.

## 13. رابط کاربری

طراحی فارسی، RTL، تمیز، حرفه‌ای، ساده، responsive و مناسب desktop/mobile؛ نه داشبورد ERP شلوغ.

صفحه اصلی انبار:

- جست‌وجوی محصول
- موجودی فعلی
- ورود کالا
- خروج کالا

جست‌وجو با SKU، نام، گروه و specificationهای پویا و در صورت مناسب autocomplete AJAX. فرم ورود/خروج چند line را پشتیبانی می‌کند اما مسیر تک‌قلم بسیار سریع است. واحد و انبار پیش‌فرض منطقی‌اند. پس از موفقیت پیام دوستانه، موجودی جدید و امکان شروع سریع عملیات بعدی نمایش داده می‌شود.

صفحات لازم:

- Authentication: Login، Logout، Change password
- Administration: Users، Roles/Permissions
- Catalog: Product groups، Specifications/Options، Products، Product units
- Inventory: Dashboard، Stock list، Receipt، Dispatch، Opening balance، Documents/Details، Edit posted، Approval queue، Reservations، Excel import
- Reports: Current stock، Movements، Document history، Low stock

Paging، filtering و ordering سمت سرور انجام می‌شوند؛ همه محصولات یکجا به مرورگر ارسال نمی‌شوند.

## 14. گزارش‌ها

1. موجودی فعلی: Product، SKU، Group، Base unit، Physical، Reserved، Available
2. گردش موجودی: Product، Document number/type، Date، Quantity، Unit، Reason
3. اسناد: Number، Type، Status، Date
4. کمبود موجودی: Product، Available stock، حداقل configurable هر محصول

همه گزارش‌ها و exportهای Excel از authorization و visibility تبعیت می‌کنند. واحدهای ناسازگار با هم جمع نمی‌شوند. حداقل موجودی تنها با threshold قابل تنظیم به ازای محصول اضافه می‌شود.

## 15. تست خودکار

حداقل پوشش لازم:

- SKU یکتا، inheritance مشخصات و جلوگیری از variant تکراری
- تبدیل واحد و precision واحد پایه
- ورود و خروج، جلوگیری از موجودی منفی و خروج هم‌زمان
- ایجاد/release رزرو و محاسبه available
- ویرایش سند Posted، حفظ revision و recalculation موجودی
- permission enforcement
- inheritance visibility گروه، override محصول و عدم نشت quantity مخفی
- opening balance Excel نامعتبر

برای transaction و concurrency مخصوص SQL Server از دیتابیس relational تست استفاده می‌شود؛ EF Core InMemory جای SQL Server فرض نمی‌شود.

## 16. ترتیب پیاده‌سازی

### فاز 1 — شالوده، هویت و دسترسی

- Solution/project structure و DI
- database configuration و entities/DbContext/Fluent Config
- migrations
- authentication، users، roles، permissions و seed
- یک vertical slice قابل اجرا با UI و تست

### فاز 2 — کاتالوگ پویا

- Units
- Product groups و hierarchy
- Specifications، options و inheritance
- Products، typed values و variant uniqueness
- UI مدیریتی و تست‌های مربوط

### فاز 3 — عملیات اصلی انبار

- Warehouse و balances
- Opening balance، Receipt و Dispatch
- approval workflow
- revision history و ویرایش سند Posted
- Reservations
- concurrency و transaction tests

### فاز 4 — مشاهده، گزارش و انتقال داده

- visibility rules
- stock screens و search
- reports
- Excel import/export

### فاز 5 — تثبیت و تحویل

- security review
- تکمیل تست خودکار
- responsive/RTL UI review
- API-readiness review
- README، schema و مستندات setup/migration

اگر ظرفیت محدود شد، فازهای زودتر باید به صورت vertical slice کاملاً runnable با migration، UI، service و test تمام شوند؛ TODO، دکمه غیرعملیاتی یا success جعلی قابل قبول نیست.

## 17. معیارهای پذیرش

- Solution build می‌شود و migrationها با موفقیت اجرا می‌شوند.
- role/permissionهای اولیه seed می‌شوند و admin با setup امن وارد می‌شود.
- admin گروه محصول و مشخصات inherited می‌سازد.
- attributeهای پویا و SKU یکتا کار می‌کنند.
- هر محصول واحدهای مختص خود را دارد.
- ورود و خروج موجودی را درست تغییر می‌دهند و موجودی فیزیکی منفی نمی‌شود.
- خروج مستقیم و approval-based هر دو کار می‌کنند.
- افراد مجاز سند Posted را ویرایش می‌کنند و نسخه‌های قبلی بازیابی‌پذیرند.
- رزرو available stock را درست تغییر می‌دهد.
- کاربران فروش فقط اطلاعات مجاز را می‌بینند و quantity در سطح محصول قابل مخفی‌سازی است.
- موجودی اولیه از Excel قابل import است.
- صفحات MVC روی desktop و mobile قابل استفاده‌اند.
- permissionها سمت سرور enforce می‌شوند.
- قواعد حیاتی کسب‌وکار تست خودکار دارند.

## 18. اقلام تحویلی نهایی

- source code کامل و قابل اجرا
- EF Core migrations و seed data
- Razor UI و Application services
- security implementation و automated tests
- `README.md`
- توضیح schema دیتابیس
- راه‌اندازی Windows و LocalDB
- روش setup امن admin اولیه
- راهنمای انتقال به SQL Server Express
- مستند تصمیم‌های مهم کسب‌وکار

## 19. تصمیم‌های اجرایی و نکات نیازمند تثبیت

این سند جهت کلی را روشن می‌کند و برای شروع کافی است. موارد زیر قبل از پیاده‌سازی هر بخش مربوطه باید به تصمیم دقیق تبدیل و در همین سند یا ADR ثبت شوند:

- نسخه دقیق .NET LTS و package versionها
- روش دقیق atomic update/concurrency در SQL Server
- الگوی شماره‌گذاری اسناد و تقویم/منطقه زمانی نمایشی
- secure default دقیق visibility (پیشنهاد: Hidden برای نقش‌های فروش)
- workflow ارتباط reservation و dispatch
- سیاست جلوگیری از duplicate opening balance
- precision استاندارد quantity، conversion factor و numeric specifications
- فرمت template Excel و روش mapping محصول (پیشنهاد اولیه: SKU)
- روش bootstrap مدیر اولیه بدون password ثابت
- تعریف threshold کمبود موجودی در مدل Product یا تنظیم مرتبط

## 20. شیوه ادامه کار

پیش از هر ماژول بزرگ، entityها، contractها، dependencyها و invariantهای آن کوتاه مشخص می‌شوند؛ سپس همان ماژول تا سطح قابل اجرا (migration + service + UI + test) پیاده‌سازی می‌شود. این فایل مرجع دامنه و نقشه راه است و با تصمیم‌های قطعی پروژه به‌روز می‌شود.

