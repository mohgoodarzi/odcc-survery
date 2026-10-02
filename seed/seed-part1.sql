/* ============================================================================
 * ODCC Survey — مجموعه‌داده‌ی نمونه‌ی فارسی (Persian sample dataset)
 *
 * این اسکریپت داده‌های واقعی و به‌هم‌پیوسته برای همه‌ی بخش‌های برنامه در
 * همان schema موجود درج می‌کند. هیچ مدل داده‌ی جدیدی ساخته نمی‌شود و
 * موجودیت‌ها از طریق کلیدهای خارجی موجود به هم متصل می‌شوند.
 *
 * مجوز اجرای مجدد: اسکریپت idempotent نیست؛ ردیف‌ها با کلیدهای ثابت درج
 * می‌شوند تا وابستگی‌ها حفظ شوند.
 * ========================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

--------------------------------------------------------------------------
-- ۱. سازمان: واحدهای سازمانی (زیرمجموعه‌ی HQ موجود)
--------------------------------------------------------------------------
DECLARE @hq UNIQUEIDENTIFIER = '01A0D412-C716-7C18-8D85-68187B170E3D';
DECLARE @orgIt  UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000001';
DECLARE @orgHr  UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000002';
DECLARE @orgSal UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000003';
DECLARE @orgOps UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000004';

INSERT INTO org_units (Id, Code, Name, Type, ParentId, Path, Level, IsActive, CreatedAt, IsDeleted)
VALUES
  (@orgIt,  N'IT',  N'فناوری اطلاعات', 3, @hq, N'/HQ/IT',  1, 1, SYSUTCDATETIME(), 0),
  (@orgHr,  N'HR',  N'منابع انسانی',    3, @hq, N'/HQ/HR',  1, 1, SYSUTCDATETIME(), 0),
  (@orgSal, N'SAL', N'فروش و بازاریابی',3, @hq, N'/HQ/SAL', 1, 1, SYSUTCDATETIME(), 0),
  (@orgOps, N'OPS', N'عملیات و پشتیبانی',3, @hq, N'/HQ/OPS', 1, 1, SYSUTCDATETIME(), 0);

--------------------------------------------------------------------------
-- ۲. کاربران (به نقش Admin موجود متصل می‌شوند تا به همه‌ی بخش‌ها دسترسی باشند)
--------------------------------------------------------------------------
DECLARE @roleAdmin UNIQUEIDENTIFIER = '7022DF9A-02F7-4496-5F44-08DF1A521553';
DECLARE @usrAdmin   UNIQUEIDENTIFIER = 'F1C472EE-C4AA-4EB2-0CD1-08DF1A521574';
DECLARE @usrManager UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000001';
DECLARE @usrHr      UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000002';
DECLARE @usrEmp     UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000003';

-- GUID‌های پایدار برای ساخت کاربر (Identity به نوع رشته‌ای نیاز ندارد)
INSERT INTO asp_net_users
  (Id, FirstName, LastName, NationalCode, AvatarUrl, IsActive, OrgUnitId, DataScope,
   CreatedAt, IsDeleted, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
   PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed,
   TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
VALUES
  (@usrManager, N'رضا', N'کریمی', N'۱۲۳۴۵۶۷۸۹۰', NULL, 1, @orgIt,  3, SYSUTCDATETIME(), 0,
   N'manager.it', N'MANAGER.IT', N'manager.it@odcc.local', N'MANAGER.IT@ODCC.LOCAL', 1,
   N'AQAAAAIAAYagAAAAEGplaceholderhashformanagituserAAAAAAAAAAAAAAAAA==',
   NEWID(), NEWID(), N'۰۹۱۲۳۴۵۶۷۸۹', 0, 0, 1, 0),
  (@usrHr, N'مریم', N'حسینی', N'۱۲۳۴۵۶۷۸۹۱', NULL, 1, @orgHr, 3, SYSUTCDATETIME(), 0,
   N'hr.specialist', N'HR.SPECIALIST', N'hr.specialist@odcc.local', N'HR.SPECIALIST@ODCC.LOCAL', 1,
   N'AQAAAAIAAYagAAAAEGplaceholderhashforhrspecialistuserAAAAAAAAAAAAA==',
   NEWID(), NEWID(), N'۰۹۱۲۳۴۵۶۷۹۰', 0, 0, 1, 0),
  (@usrEmp, N'علی', N'محمدی', N'۱۲۳۴۵۶۷۸۹۲', NULL, 1, @orgSal, 0, SYSUTCDATETIME(), 0,
   N'employee.sales', N'EMPLOYEE.SALES', N'employee.sales@odcc.local', N'EMPLOYEE.SALES@ODCC.LOCAL', 1,
   N'AQAAAAIAAYagAAAAEGplaceholderhashforemployeesalesuserAAAAAAAAAAAAA==',
   NEWID(), NEWID(), N'۰۹۱۲۳۴۵۶۷۹۱', 0, 0, 1, 0);

INSERT INTO asp_net_user_roles (UserId, RoleId) VALUES
  (@usrManager, @roleAdmin),
  (@usrHr,      @roleAdmin),
  (@usrEmp,     @roleAdmin);

--------------------------------------------------------------------------
-- ۳. پست‌های سازمانی
--------------------------------------------------------------------------
DECLARE @posItMgr  UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000001';
DECLARE @posHrSpec UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000002';
DECLARE @posSales  UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000003';
DECLARE @posOps    UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000004';
-- موقعیت‌های جدید برای نمونه‌کارمندان
DECLARE @posItDev  UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000005';
DECLARE @posItNet  UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000006';
DECLARE @posHrPay  UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000007';
DECLARE @posSalMgr UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000008';
DECLARE @posOpsLog UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000009';

INSERT INTO positions (Id, Code, Title, OrgUnitId, ReportsToPositionId, Grade, IsActive, Headcount, Description, CreatedAt, IsDeleted)
VALUES
  (@posItMgr,  N'POS-IT-MGR',  N'مدیر فناوری اطلاعات',   @orgIt,  NULL,      12, 1, 1, N'مسئول زیرساخت و نرم‌افزار', SYSUTCDATETIME(), 0),
  (@posHrSpec, N'POS-HR-SPC',  N'کارشناس منابع انسانی',   @orgHr,  NULL,       8, 1, 2, N'مسئول جذب و آموزش',         SYSUTCDATETIME(), 0),
  (@posSales,  N'POS-SAL-REP', N'کارشناس فروش',           @orgSal, NULL,       6, 1, 5, N'فروش و ارتباط با مشتری',    SYSUTCDATETIME(), 0),
  (@posOps,    N'POS-OPS-SPC', N'کارشناس پشتیبانی',       @orgOps, NULL,       5, 1, 3, N'پشتیبانی کاربران',          SYSUTCDATETIME(), 0),
  (@posItDev,  N'POS-IT-DEV',  N'توسعه‌دهنده نرم‌افزار',   @orgIt,  @posItMgr, 9, 1, 4, N'توسعه و نگهداری نرم‌افزار',  SYSUTCDATETIME(), 0),
  (@posItNet,  N'POS-IT-NET',  N'کارشناس شبکه و زیرساخت', @orgIt,  @posItMgr, 7, 1, 2, N'شبکه، سرور و امنیت',        SYSUTCDATETIME(), 0),
  (@posHrPay,  N'POS-HR-PAY',  N'کارشناس حقوق و دستمزد',  @orgHr,  @posHrSpec,7, 1, 2, N'حقوق، دستمزد و بیمه',       SYSUTCDATETIME(), 0),
  (@posSalMgr, N'POS-SAL-MGR', N'مدیر فروش',              @orgSal, NULL,      11, 1, 1, N'رهبری تیم فروش',            SYSUTCDATETIME(), 0),
  (@posOpsLog, N'POS-OPS-LOG', N'کارشناس انبار و لجستیک', @orgOps, @posOps,    4, 1, 3, N'انبار، ارسال و موجودی',     SYSUTCDATETIME(), 0);

--------------------------------------------------------------------------
-- ۴. کارمندان (مدیر، کارشناس منابع انسانی، کارمند فروش و چند کارمند دیگر)
--------------------------------------------------------------------------
DECLARE @empAdmin   UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000001';
DECLARE @empManager UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000002';
DECLARE @empHr      UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000003';
DECLARE @empSales1  UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000004';
DECLARE @empSales2  UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000005';
DECLARE @empOps1    UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000006';

INSERT INTO employees
  (Id, EmployeeCode, NationalCode, FirstName, LastName, FatherName, UserId, OrgUnitId,
   PositionId, ManagerId, Status, StartDate, EndDate, WorkEmail, InternalPhone,
   CreatedAt, IsDeleted)
VALUES
  (@empAdmin,   N'EMP-0001', N'1000000001', N'مدیر',     N'سامانه',  N'سیستم',      @usrAdmin,   @hq,    NULL,         NULL,        1, N'2020-01-01', NULL, N'admin@odcc.local',     N'1001', SYSUTCDATETIME(), 0),
  (@empManager, N'EMP-0002', N'1000000002', N'رضا',      N'کریمی',    N'محمد',       @usrManager, @orgIt, @posItMgr,    NULL,        1, N'2018-03-15', NULL, N'manager.it@odcc.local',N'1002', SYSUTCDATETIME(), 0),
  (@empHr,      N'EMP-0003', N'1000000003', N'مریم',     N'حسینی',    N'ابراهیم',    @usrHr,      @orgHr, @posHrSpec,   NULL,        1, N'2019-09-01', NULL, N'hr.specialist@odcc.local', N'1003', SYSUTCDATETIME(), 0),
  (@empSales1,  N'EMP-0004', N'1000000004', N'علی',      N'محمدی',    N'حسن',        @usrEmp,     @orgSal,@posSales,    NULL,        1, N'2021-06-20', NULL, N'employee.sales@odcc.local', N'1004', SYSUTCDATETIME(), 0),
  (@empSales2,  N'EMP-0005', N'1000000005', N'زهرا',     N'احمدی',    N'رضا',        NULL,        @orgSal,@posSales,    NULL,        1, N'2022-02-10', NULL, N'z.ahmadi@odcc.local',  N'1005', SYSUTCDATETIME(), 0),
  (@empOps1,    N'EMP-0006', N'1000000006', N'حسین',     N'رضایی',    N'علی',        NULL,        @orgOps,@posOps,      NULL,        1, N'2020-11-05', NULL, N'h.rezaei@odcc.local',  N'1006', SYSUTCDATETIME(), 0);

--------------------------------------------------------------------------
-- ۴-ب. ۲۰ کارمند نمونه (داده‌ی واقعی برای دمو و تست جریان ورود از اکسل)
--
-- این ردیف‌ها دقیقاً همان داده‌ی seed/employees-sample.xlsx هستند تا مسیر
-- «الدست اکسل ← API ← پایگاه داده» قابل بازتولید باشد. کد ملی‌ها لاتین‌رقم
-- نوشته شده‌اند تا اعتبارسنجی [0-9]{10} همانند مسیر API برقرار بماند.
--------------------------------------------------------------------------
INSERT INTO employees
  (Id, EmployeeCode, NationalCode, FirstName, LastName, FatherName, UserId, OrgUnitId,
   PositionId, ManagerId, Status, StartDate, EndDate, WorkEmail, InternalPhone,
   CreatedAt, IsDeleted)
VALUES
  -- فناوری اطلاعات (مدیر: EMP-0002)
  ('44444444-0000-0000-0000-000000000007', N'EMP-0007', N'1000000007', N'سارا',   N'موسوی',    N'محمود',   NULL, @orgIt, @posItDev, @empManager, 1, N'2019-04-01', NULL,         N's.mousavi@odcc.local',     N'1007', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000008', N'EMP-0008', N'1000000008', N'محمد',   N'رحیمی',    N'حسن',     NULL, @orgIt, @posItDev, @empManager, 1, N'2020-08-15', NULL,         N'm.rahimi@odcc.local',      N'1008', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000009', N'EMP-0009', N'1000000009', N'فاطمه',  N'نجفی',     N'علی',     NULL, @orgIt, @posItNet, @empManager, 1, N'2021-02-01', NULL,         N'f.najafi@odcc.local',      N'1009', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000010', N'EMP-0010', N'1000000010', N'امیر',   N'کاظمی',    N'رضا',     NULL, @orgIt, @posItNet, @empManager, 2, N'2018-11-20', NULL,         N'a.kazemi@odcc.local',      N'1010', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000011', N'EMP-0011', N'1000000011', N'نگار',   N'شیرازی',   N'مهدی',    NULL, @orgIt, @posItDev, @empManager, 1, N'2023-06-01', NULL,         N'n.shirazi@odcc.local',     N'1011', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000024', N'EMP-0024', N'1000000024', N'بابک',   N'سلطانی',   N'همایون',  NULL, @orgIt, @posItDev, @empManager, 1, N'2023-11-01', NULL,         N'b.soltani@odcc.local',     N'1024', SYSUTCDATETIME(), 0),
  -- منابع انسانی (مدیر: EMP-0003)
  ('44444444-0000-0000-0000-000000000012', N'EMP-0012', N'1000000012', N'زینب',   N'ابراهیمی', N'کریم',    NULL, @orgHr, @posHrPay, @empHr,      1, N'2019-10-10', NULL,         N'z.ebrahimi@odcc.local',    N'1012', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000013', N'EMP-0013', N'1000000013', N'مهدی',   N'صادقی',    N'جواد',    NULL, @orgHr, @posHrSpec, @empHr,     1, N'2022-03-15', NULL,         N'm.sadeghi@odcc.local',     N'1013', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000014', N'EMP-0014', N'1000000014', N'الهام',  N'رستمی',    N'فرهاد',   NULL, @orgHr, @posHrPay, @empHr,      3, N'2020-12-01', NULL,         N'e.rostami@odcc.local',     N'1014', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000025', N'EMP-0025', N'1000000025', N'رویا',   N'احمدزاده', N'پرویز',   NULL, @orgHr, @posHrSpec, @empHr,     1, N'2024-01-15', NULL,         N'r.ahmadzadeh@odcc.local',  N'1025', SYSUTCDATETIME(), 0),
  -- فروش و بازاریابی (مدیر فروش: EMP-0015)
  ('44444444-0000-0000-0000-000000000015', N'EMP-0015', N'1000000015', N'بهمن',   N'قاسمی',    N'نادر',    NULL, @orgSal, @posSalMgr, @empAdmin,  1, N'2017-05-01', NULL,         N'b.ghasemi@odcc.local',     N'1015', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000016', N'EMP-0016', N'1000000016', N'سمیرا',  N'یوسفی',    N'اصغر',    NULL, @orgSal, @posSales,  '44444444-0000-0000-0000-000000000015', 1, N'2021-09-01', NULL, N's.yousefi@odcc.local', N'1016', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000017', N'EMP-0017', N'1000000017', N'کاوه',   N'عباسی',    N'منصور',   NULL, @orgSal, @posSales,  '44444444-0000-0000-0000-000000000015', 1, N'2022-01-10', NULL, N'k.abbasi@odcc.local', N'1017', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000018', N'EMP-0018', N'1000000018', N'مریم',   N'علوی',     N'سعید',    NULL, @orgSal, @posSales,  '44444444-0000-0000-0000-000000000015', 4, N'2019-04-15', N'2023-08-31', N'm.alavi@odcc.local', N'1018', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000019', N'EMP-0019', N'1000000019', N'آرش',   N'مقدم',     N'یدالله',  NULL, @orgSal, @posSales,  '44444444-0000-0000-0000-000000000015', 1, N'2023-02-20', NULL, N'a.moghadam@odcc.local', N'1019', SYSUTCDATETIME(), 0),
  -- عملیات و پشتیبانی (مدیر: EMP-0006)
  ('44444444-0000-0000-0000-000000000020', N'EMP-0020', N'1000000020', N'هما',    N'جعفری',    N'میرزا',   NULL, @orgOps, @posOps,    @empOps1,    1, N'2020-05-12', NULL,         N'h.jafari@odcc.local',      N'1020', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000021', N'EMP-0021', N'1000000021', N'کامران', N'فرهادی',   N'بهمن',    NULL, @orgOps, @posOpsLog, @empOps1,    1, N'2021-07-05', NULL,         N'k.farhadi@odcc.local',     N'1021', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000022', N'EMP-0022', N'1000000022', N'لیلا',   N'حسن‌زاده', N'علی‌اکبر', NULL, @orgOps, @posOpsLog, @empOps1,    1, N'2022-10-01', NULL,         N'l.hassanzadeh@odcc.local', N'1022', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000023', N'EMP-0023', N'1000000023', N'پرویز',  N'نوری',     N'غلام',    NULL, @orgOps, @posOps,    @empOps1,    2, N'2019-01-18', NULL,         N'p.nouri@odcc.local',       N'1023', SYSUTCDATETIME(), 0),
  ('44444444-0000-0000-0000-000000000026', N'EMP-0026', N'1000000026', N'نیما',   N'گلی',      N'فرید',    NULL, @orgOps, @posOpsLog, @empOps1,    1, N'2024-03-01', NULL,         N'n.goli@odcc.local',        N'1026', SYSUTCDATETIME(), 0);

-- کارشناسان فروش قدیمی حالا به مدیر فروش (EMP-0015) گزارش می‌دهند.
DECLARE @empSalesMgr UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000015';
UPDATE employees SET ManagerId = @empSalesMgr WHERE Id IN (@empSales1, @empSales2);

-- به‌روزرسانی واحد سازمانی ریشه به مدیر کارمند ریشه
UPDATE org_units SET ManagerEmployeeId = @empManager WHERE Id = @hq;
UPDATE org_units SET ManagerEmployeeId = @empManager WHERE Id = @orgIt;
UPDATE org_units SET ManagerEmployeeId = @empHr      WHERE Id = @orgHr;
UPDATE org_units SET ManagerEmployeeId = @empSalesMgr WHERE Id = @orgSal;
UPDATE org_units SET ManagerEmployeeId = @empOps1    WHERE Id = @orgOps;

--------------------------------------------------------------------------
-- ۵. کتابخانه‌ی سؤالات: سؤالات، نسخه‌ها، گزینه‌ها، ترجمه‌ها و برچسب‌ها
--------------------------------------------------------------------------
DECLARE @qNps     UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000001';
DECLARE @qCsat    UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000002';
DECLARE @qEtype   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000003';
DECLARE @qImprove UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000004';
DECLARE @qTools   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000005';
DECLARE @qComment UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000006';

INSERT INTO questions (Id, Code, Type, ScaleMax, IsArchived, CurrentVersionNumber, CreatedAt, IsDeleted)
VALUES
  (@qNps,     N'Q-NPS',     3, 10, 0, 1, SYSUTCDATETIME(), 0),
  (@qCsat,    N'Q-CSAT',    3,  5, 0, 1, SYSUTCDATETIME(), 0),
  (@qEtype,   N'Q-ETYPE',   1,  5, 0, 1, SYSUTCDATETIME(), 0),
  (@qImprove, N'Q-IMPROVE', 2,  5, 0, 1, SYSUTCDATETIME(), 0),
  (@qTools,   N'Q-TOOLS',   4,  5, 0, 1, SYSUTCDATETIME(), 0),
  (@qComment, N'Q-COMMENT', 6,  5, 0, 1, SYSUTCDATETIME(), 0);

INSERT INTO question_versions (Id, QuestionId, VersionNumber, Snapshot, ChangeSummary, CreatedByUserId, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @qNps,     1, N'{"code":"Q-NPS","text":"احتمال اینکه این سازمان را به دوستان خود توصیه می‌کنید چقدر است؟","scaleMax":10}', N'نسخه‌ی اولیه', @usrAdmin, SYSUTCDATETIME(), 0),
  (NEWID(), @qCsat,    1, N'{"code":"Q-CSAT","text":"میزان رضایت کلی شما از محیط کار چقدر است؟","scaleMax":5}', N'نسخه‌ی اولیه', @usrAdmin, SYSUTCDATETIME(), 0),
  (NEWID(), @qEtype,   1, N'{"code":"Q-ETYPE","text":"نوع قرارداد شما چیست؟","options":["تمام‌وقت","پاره‌وقت","پیمانی"]}', N'نسخه‌ی اولیه', @usrAdmin, SYSUTCDATETIME(), 0),
  (NEWID(), @qImprove, 1, N'{"code":"Q-IMPROVE","text":"چه حوزه‌هایی نیاز به بهبود دارد؟","options":["حقوق","محیط","ابزارها","مدیریت","آموزش"]}', N'نسخه‌ی اولیه', @usrAdmin, SYSUTCDATETIME(), 0),
  (NEWID(), @qTools,   1, N'{"code":"Q-TOOLS","text":"آیا ابزارهای نرم‌افزاری شما کافی است؟"}', N'نسخه‌ی اولیه', @usrAdmin, SYSUTCDATETIME(), 0),
  (NEWID(), @qComment, 1, N'{"code":"Q-COMMENT","text":"پیشنهادهای شما برای بهبود سازمان"}', N'نسخه‌ی اولیه', @usrAdmin, SYSUTCDATETIME(), 0);

INSERT INTO question_localizations (Id, QuestionId, Language, Text, Description)
VALUES
  (NEWID(), @qNps,     1, N'احتمال اینکه این سازمان را به دوستان خود توصیه می‌کنید چقدر است؟', N'از عدد ۰ تا ۱۰ انتخاب کنید'),
  (NEWID(), @qNps,     2, N'How likely are you to recommend this organization to others?', NULL),
  (NEWID(), @qCsat,    1, N'میزان رضایت کلی شما از محیط کار چقدر است؟', N'از عدد ۱ تا ۵ انتخاب کنید'),
  (NEWID(), @qCsat,    2, N'How satisfied are you with your work environment?', NULL),
  (NEWID(), @qEtype,   1, N'نوع قرارداد شما چیست؟', NULL),
  (NEWID(), @qEtype,   2, N'What is your contract type?', NULL),
  (NEWID(), @qImprove, 1, N'چه حوزه‌هایی نیاز به بهبود دارد؟', N'می‌توانید چند گزینه را انتخاب کنید'),
  (NEWID(), @qImprove, 2, N'Which areas need improvement?', NULL),
  (NEWID(), @qTools,   1, N'آیا ابزارهای نرم‌افزاری شما برای انجام کار کافی است؟', NULL),
  (NEWID(), @qTools,   2, N'Are your software tools sufficient for your work?', NULL),
  (NEWID(), @qComment, 1, N'پیشنهادهای شما برای بهبود سازمان', N'پاسخ دلخواه و اختیاری'),
  (NEWID(), @qComment, 2, N'Your suggestions for improving the organization', NULL);

-- گزینه‌ها برای سؤالات انتخابی
DECLARE @oEtypeFull    UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000001';
DECLARE @oEtypePart    UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000002';
DECLARE @oEtypeCon     UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000003';
DECLARE @oImproveSal   UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000004';
DECLARE @oImproveEnv   UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000005';
DECLARE @oImproveTool  UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000006';
DECLARE @oImproveMgmt  UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000007';
DECLARE @oImproveTrain UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000008';

INSERT INTO question_options (Id, Code, QuestionId, DisplayOrder, CreatedAt, IsDeleted)
VALUES
  (@oEtypeFull,    N'FULLTIME', @qEtype,   1, SYSUTCDATETIME(), 0),
  (@oEtypePart,    N'PARTTIME', @qEtype,   2, SYSUTCDATETIME(), 0),
  (@oEtypeCon,     N'CONTRACT', @qEtype,   3, SYSUTCDATETIME(), 0),
  (@oImproveSal,   N'SALARY',   @qImprove, 1, SYSUTCDATETIME(), 0),
  (@oImproveEnv,   N'ENVIRON',  @qImprove, 2, SYSUTCDATETIME(), 0),
  (@oImproveTool,  N'TOOLS',    @qImprove, 3, SYSUTCDATETIME(), 0),
  (@oImproveMgmt,  N'MANAGE',   @qImprove, 4, SYSUTCDATETIME(), 0),
  (@oImproveTrain, N'TRAINING', @qImprove, 5, SYSUTCDATETIME(), 0);

INSERT INTO question_option_localizations (Id, QuestionOptionId, Language, Text)
VALUES
  (NEWID(), @oEtypeFull,    1, N'تمام‌وقت'), (NEWID(), @oEtypeFull,    2, N'Full-time'),
  (NEWID(), @oEtypePart,    1, N'پاره‌وقت'), (NEWID(), @oEtypePart,    2, N'Part-time'),
  (NEWID(), @oEtypeCon,     1, N'پیمانی'),  (NEWID(), @oEtypeCon,     2, N'Contract'),
  (NEWID(), @oImproveSal,   1, N'حقوق و دستمزد'), (NEWID(), @oImproveSal,   2, N'Salary'),
  (NEWID(), @oImproveEnv,   1, N'محیط کار'),      (NEWID(), @oImproveEnv,   2, N'Work environment'),
  (NEWID(), @oImproveTool,  1, N'ابزارها'),       (NEWID(), @oImproveTool,  2, N'Tools'),
  (NEWID(), @oImproveMgmt,  1, N'مدیریت'),        (NEWID(), @oImproveMgmt,  2, N'Management'),
  (NEWID(), @oImproveTrain, 1, N'آموزش'),         (NEWID(), @oImproveTrain, 2, N'Training');

INSERT INTO question_tags (Id, QuestionId, Name, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @qNps,     N'شاخص‌ کلیدی', SYSUTCDATETIME(), 0),
  (NEWID(), @qCsat,    N'رضایت',       SYSUTCDATETIME(), 0),
  (NEWID(), @qImprove, N'بهبود',       SYSUTCDATETIME(), 0);

--------------------------------------------------------------------------
-- ۶. پرسشنامه: بخش‌ها، آیتم‌ها، ترجمه‌ها و قوانین انشعاب
--------------------------------------------------------------------------
DECLARE @qrOrg   UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000001';
DECLARE @secMain UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000010';
DECLARE @secImpr UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000011';

DECLARE @itNps     UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000020';
DECLARE @itCsat    UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000021';
DECLARE @itEtype   UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000022';
DECLARE @itImprove UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000023';
DECLARE @itTools   UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000024';
DECLARE @itComment UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000025';

INSERT INTO questionnaires (Id, Code, Status, Version, CreatedAt, IsDeleted)
VALUES (@qrOrg, N'QR-ORG-01', 2, 1, SYSUTCDATETIME(), 0);

INSERT INTO questionnaire_localizations (Id, QuestionnaireId, Language, Title, Description)
VALUES
  (NEWID(), @qrOrg, 1, N'پرسشنامه فرهنگ و محیط سازمانی', N'سنجش رضایت کارکنان در حوزه‌های مختلف'),
  (NEWID(), @qrOrg, 2, N'Organizational Culture Survey',   N'Measuring employee satisfaction across areas');

INSERT INTO questionnaire_sections (Id, QuestionnaireId, DisplayOrder, IsOptional, CreatedAt, IsDeleted)
VALUES
  (@secMain, @qrOrg, 1, 0, SYSUTCDATETIME(), 0),
  (@secImpr, @qrOrg, 2, 1, SYSUTCDATETIME(), 0);

INSERT INTO questionnaire_section_localizations (Id, SectionId, Language, Title)
VALUES
  (NEWID(), @secMain, 1, N'بخش اول: سنجش کلی'), (NEWID(), @secMain, 2, N'Section One: General'),
  (NEWID(), @secImpr, 1, N'بخش دوم: حوزه‌های بهبود'), (NEWID(), @secImpr, 2, N'Section Two: Improvement Areas');

INSERT INTO questionnaire_items
  (Id, SectionId, QuestionId, QuestionVersionNumber, QuestionCode, QuestionType,
   DisplayOrder, IsRequired, TitleOverride, CreatedAt, IsDeleted)
VALUES
  (@itNps,     @secMain, @qNps,     1, N'Q-NPS',     3, 1, 1, NULL, SYSUTCDATETIME(), 0),
  (@itCsat,    @secMain, @qCsat,    1, N'Q-CSAT',    3, 2, 1, NULL, SYSUTCDATETIME(), 0),
  (@itEtype,   @secMain, @qEtype,   1, N'Q-ETYPE',   1, 3, 1, NULL, SYSUTCDATETIME(), 0),
  (@itImprove, @secImpr, @qImprove, 1, N'Q-IMPROVE', 2, 1, 0, NULL, SYSUTCDATETIME(), 0),
  (@itTools,   @secImpr, @qTools,   1, N'Q-TOOLS',   4, 2, 1, NULL, SYSUTCDATETIME(), 0),
  (@itComment, @secImpr, @qComment, 1, N'Q-COMMENT', 6, 3, 0, NULL, SYSUTCDATETIME(), 0);

-- قانون انشعاب: اگر پاسخ «خیر» به سؤال ابزارها → به سراغ بخش پیشنهادها
INSERT INTO questionnaire_branching_rules
  (Id, ItemId, TargetItemId, Condition, ExpectedValue, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @itTools, @itComment, 1, N'no', SYSUTCDATETIME(), 0);

--------------------------------------------------------------------------
-- ۷. نظرسنجی‌ها (فعال، زمان‌بندی‌شده و ناشناس) به‌همراه ترجمه‌ها
--------------------------------------------------------------------------
DECLARE @svAnnual   UNIQUEIDENTIFIER = '88888888-0000-0000-0000-000000000001';
DECLARE @svWorkpl   UNIQUEIDENTIFIER = '88888888-0000-0000-0000-000000000002';
DECLARE @svTools    UNIQUEIDENTIFIER = '88888888-0000-0000-0000-000000000003';

-- قالب نظرسنجی
DECLARE @tplGeneral UNIQUEIDENTIFIER = '88888888-0000-0000-0000-000000000090';

INSERT INTO survey_templates
  (Id, Code, Status, QuestionnaireId, QuestionnaireCode, IsAnonymous, AllowEditResponse,
   ShowProgressBar, SingleResponsePerUser, EstimatedMinutes, CreatedAt, IsDeleted)
VALUES
  (@tplGeneral, N'TPL-GENERAL', 1, @qrOrg, N'QR-ORG-01', 0, 1, 1, 1, 8, SYSUTCDATETIME(), 0);

INSERT INTO survey_template_localizations (Id, TemplateId, Language, Title, Description)
VALUES
  (NEWID(), @tplGeneral, 1, N'قالب عمومی نظرسنجی کارکنان', N'قالب آماده برای نظرسنجی‌های دوره‌ای'),
  (NEWID(), @tplGeneral, 2, N'General Employee Survey Template', N'Ready-made template for periodic surveys');

INSERT INTO surveys
  (Id, Code, Status, QuestionnaireId, QuestionnaireVersion, QuestionnaireCode, TemplateId,
   IsAnonymous, AllowEditResponse, ShowProgressBar, SingleResponsePerUser,
   StartDate, EndDate, EstimatedMinutes, PublishedAt, ActivatedAt, ClosedAt, ArchivedAt,
   CreatedAt, IsDeleted)
VALUES
  (@svAnnual, N'S-1404-ANNUAL', 3, @qrOrg, 1, N'QR-ORG-01', @tplGeneral,
   0, 1, 1, 1,
   DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, 16, SYSUTCDATETIME()), 8,
   DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, -14, SYSUTCDATETIME()), NULL, NULL,
   DATEADD(DAY, -15, SYSUTCDATETIME()), 0),
  (@svWorkpl, N'S-1404-WORKPLACE', 2, @qrOrg, 1, N'QR-ORG-01', NULL,
   0, 0, 1, 1,
   DATEADD(DAY, 5, SYSUTCDATETIME()), DATEADD(DAY, 35, SYSUTCDATETIME()), 6,
   DATEADD(DAY, -1, SYSUTCDATETIME()), NULL, NULL, NULL,
   DATEADD(DAY, -2, SYSUTCDATETIME()), 0),
  (@svTools, N'S-1404-TOOLS', 3, @qrOrg, 1, N'QR-ORG-01', NULL,
   1, 0, 1, 0,
   DATEADD(DAY, -10, SYSUTCDATETIME()), DATEADD(DAY, 20, SYSUTCDATETIME()), 5,
   DATEADD(DAY, -10, SYSUTCDATETIME()), DATEADD(DAY, -10, SYSUTCDATETIME()), NULL, NULL,
   DATEADD(DAY, -11, SYSUTCDATETIME()), 0);

INSERT INTO survey_localizations (Id, SurveyId, Language, Title, Description, WelcomeMessage, ThankYouMessage)
VALUES
  (NEWID(), @svAnnual, 1, N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴',
   N'این نظرسنجی برای سنجش رضایت کارکنان از محیط کار، ابزارها و مدیریت برگزار می‌شود.',
   N'با سلام و احترام، از شما دعوت می‌شود به این نظرسنجی پاسخ دهید. پاسخ‌های شما محرمانه است.',
   N'از مشارکت شما سپاسگزاریم.'),
  (NEWID(), @svAnnual, 2, N'Annual Employee Satisfaction Survey 1404',
   N'This survey measures employee satisfaction with the work environment, tools and management.',
   N'Please take a moment to complete this survey. Your answers are confidential.',
   N'Thank you for participating.'),
  (NEWID(), @svWorkpl, 1, N'نظرسنجی محیط کار و ایمنی',
   N'بررسی شرایط محیط کار و ایمنی محل کار', N'لطفاً با دقت پاسخ دهید.', N'ممنون از همکاری شما.'),
  (NEWID(), @svWorkpl, 2, N'Workplace Environment Survey', N'Review of workplace conditions', NULL, NULL),
  (NEWID(), @svTools, 1, N'نظرسنجی ابزارها و نرم‌افزارها',
   N'سنجش کفایت ابزارهای نرم‌افزاری', N'پاسخ‌های شما ناشناس است.', N'سپاس'),
  (NEWID(), @svTools, 2, N'Tools and Software Survey', N'Assessing software tools adequacy', NULL, NULL);

--------------------------------------------------------------------------
-- ۸. کمپین‌ها: تعریف، ترجمه‌ها، مخاطبان، توزیع‌ها و یادآورها
--------------------------------------------------------------------------
DECLARE @cmEmail UNIQUEIDENTIFIER = '99999999-0000-0000-0000-000000000001';
DECLARE @cmSms   UNIQUEIDENTIFIER = '99999999-0000-0000-0000-000000000002';
DECLARE @cmInApp UNIQUEIDENTIFIER = '99999999-0000-0000-0000-000000000003';

INSERT INTO campaigns
  (Id, Code, Status, SurveyId, SurveyCode, AudienceType, IncludeInactiveEmployees, Channel,
   ScheduledAt, EndsAt, StartedAt, CompletedAt, ArchivedAt, CreatedAt, IsDeleted,
   OrgUnitId, OrgUnitPath)
VALUES
  (@cmEmail, N'C-1404-EMAIL', 3, @svAnnual, N'S-1404-ANNUAL', 2, 0, 1,
   DATEADD(DAY, -13, SYSUTCDATETIME()), DATEADD(DAY, 17, SYSUTCDATETIME()),
   DATEADD(DAY, -13, SYSUTCDATETIME()), NULL, NULL,
   DATEADD(DAY, -14, SYSUTCDATETIME()), 0, @hq, N'/HQ'),
  (@cmSms, N'C-1404-SMS', 2, @svAnnual, N'S-1404-ANNUAL', 3, 0, 2,
   DATEADD(DAY, 2, SYSUTCDATETIME()), DATEADD(DAY, 20, SYSUTCDATETIME()),
   NULL, NULL, NULL, DATEADD(DAY, -1, SYSUTCDATETIME()), 0, @hq, N'/HQ'),
  (@cmInApp, N'C-1404-INAPP', 4, @svTools, N'S-1404-TOOLS', 1, 0, 3,
   DATEADD(DAY, -9, SYSUTCDATETIME()), DATEADD(DAY, 1, SYSUTCDATETIME()),
   DATEADD(DAY, -9, SYSUTCDATETIME()), DATEADD(DAY, -2, SYSUTCDATETIME()), NULL,
   DATEADD(DAY, -10, SYSUTCDATETIME()), 0, @hq, N'/HQ');

INSERT INTO campaign_localizations (Id, CampaignId, Language, Title, Description)
VALUES
  (NEWID(), @cmEmail, 1, N'کمپین ایمیل نظرسنجی سالانه',
   N'ارسال دعوت‌نامه‌ی نظرسنجی سالانه از طریق ایمیل به کارکنان'),
  (NEWID(), @cmEmail, 2, N'Annual Survey Email Campaign', N'Email invitations for the annual survey'),
  (NEWID(), @cmSms, 1, N'یادآور پیامکی نظرسنجی سالانه',
   N'ارسال یادآور از طریق پیامک برای شرکت‌کنندگان دعوت‌شده'),
  (NEWID(), @cmSms, 2, N'SMS Reminder Campaign', N'SMS reminders for invited participants'),
  (NEWID(), @cmInApp, 1, N'اعلان درون‌برنامه‌ای نظرسنجی ابزارها',
   N'اطلاع‌رسانی درون‌برنامه‌ای برای نظرسنجی ابزارها'),
  (NEWID(), @cmInApp, 2, N'In-App Notification Campaign', N'In-app notification for the tools survey');

INSERT INTO campaign_target_units (Id, CampaignId, OrgUnitId, IncludeDescendants, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @cmEmail, @orgIt,  1, SYSUTCDATETIME(), 0),
  (NEWID(), @cmEmail, @orgSal, 1, SYSUTCDATETIME(), 0);

INSERT INTO campaign_target_members (Id, CampaignId, EmployeeId, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @cmEmail, @empHr,      SYSUTCDATETIME(), 0),
  (NEWID(), @cmEmail, @empOps1,    SYSUTCDATETIME(), 0),
  (NEWID(), @cmSms,   @empSales1,  SYSUTCDATETIME(), 0),
  (NEWID(), @cmSms,   @empSales2,  SYSUTCDATETIME(), 0);

INSERT INTO campaign_distributions
  (Id, CampaignId, EmployeeId, WorkEmail, Status, SentAt, RespondedAt, FailureReason,
   ReminderCount, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @cmEmail, @empAdmin,   N'admin@odcc.local',          2, DATEADD(DAY, -13, SYSUTCDATETIME()), NULL,                    NULL, 1, DATEADD(DAY, -13, SYSUTCDATETIME()), 0),
  (NEWID(), @cmEmail, @empManager, N'manager.it@odcc.local',     4, DATEADD(DAY, -13, SYSUTCDATETIME()), DATEADD(DAY, -10, SYSUTCDATETIME()), NULL, 0, DATEADD(DAY, -13, SYSUTCDATETIME()), 0),
  (NEWID(), @cmEmail, @empHr,      N'hr.specialist@odcc.local',  4, DATEADD(DAY, -13, SYSUTCDATETIME()), DATEADD(DAY, -9,  SYSUTCDATETIME()), NULL, 0, DATEADD(DAY, -13, SYSUTCDATETIME()), 0),
  (NEWID(), @cmEmail, @empSales1,  N'employee.sales@odcc.local', 2, DATEADD(DAY, -13, SYSUTCDATETIME()), NULL,                    NULL, 1, DATEADD(DAY, -13, SYSUTCDATETIME()), 0),
  (NEWID(), @cmEmail, @empSales2,  N'z.ahmadi@odcc.local',       3, DATEADD(DAY, -13, SYSUTCDATETIME()), NULL,                    N'آدرس ایمیل نامعتبر', 0, DATEADD(DAY, -13, SYSUTCDATETIME()), 0),
  (NEWID(), @cmEmail, @empOps1,    N'h.rezaei@odcc.local',       2, DATEADD(DAY, -13, SYSUTCDATETIME()), NULL,                    NULL, 0, DATEADD(DAY, -13, SYSUTCDATETIME()), 0);

DECLARE @rmd1 UNIQUEIDENTIFIER = '99999999-0000-0000-0000-000000000010';
DECLARE @rmd2 UNIQUEIDENTIFIER = '99999999-0000-0000-0000-000000000011';

INSERT INTO campaign_reminders (Id, CampaignId, SendAt, Status, SentAt, CreatedAt, IsDeleted)
VALUES
  (@rmd1, @cmEmail, DATEADD(DAY, -6, SYSUTCDATETIME()), 2, DATEADD(DAY, -6, SYSUTCDATETIME()), DATEADD(DAY, -13, SYSUTCDATETIME()), 0),
  (@rmd2, @cmEmail, DATEADD(DAY, -2, SYSUTCDATETIME()), 2, DATEADD(DAY, -2, SYSUTCDATETIME()), DATEADD(DAY, -13, SYSUTCDATETIME()), 0);

INSERT INTO campaign_reminder_localizations (Id, ReminderId, Language, Subject, Body)
VALUES
  (NEWID(), @rmd1, 1, N'یادآوری: نظرسنجی سالانه هنوز کامل نشده است',
   N'کاربر گرامی، فرصت پاسخ‌گویی به نظرسنجی سالانه در حال اتمام است. لطفاً در اولین فرصت پاسخ دهید.'),
  (NEWID(), @rmd1, 2, N'Reminder: Annual survey not completed',
   N'Dear colleague, the annual survey is closing soon. Please respond at your earliest convenience.'),
  (NEWID(), @rmd2, 1, N'یادآوری نهایی: نظرسنجی سالانه',
   N'این آخرین یادآوری برای شرکت در نظرسنجی سالانه است. از مشارکت شما سپاسگزاریم.');

COMMIT TRANSACTION;
PRINT N'بخش اول داده‌ها (سازمان، کاربران، پرسشنامه، نظرسنجی، کمپین) درج شد.';
