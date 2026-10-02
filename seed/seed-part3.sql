/* ============================================================================
 * ODCC Survey — مجموعه‌داده‌ی نمونه‌ی فارسی — بخش سوم
 * گزارش‌ها، برنامه‌های اقدام، گردش کار، یکپارچه‌سازی‌ها و اعلان‌ها
 * ========================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @svAnnual   UNIQUEIDENTIFIER = '88888888-0000-0000-0000-000000000001';
DECLARE @cmEmail    UNIQUEIDENTIFIER = '99999999-0000-0000-0000-000000000001';
DECLARE @cmInApp    UNIQUEIDENTIFIER = '99999999-0000-0000-0000-000000000003';
DECLARE @usrAdmin   UNIQUEIDENTIFIER = 'F1C472EE-C4AA-4EB2-0CD1-08DF1A521574';
DECLARE @usrManager UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000001';
DECLARE @usrHr      UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000002';
DECLARE @usrEmp     UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000003';
DECLARE @orgIt      UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000001';
DECLARE @orgHr      UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000002';
DECLARE @orgSal     UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000003';
DECLARE @hq         UNIQUEIDENTIFIER = '01A0D412-C716-7C18-8D85-68187B170E3D';

--------------------------------------------------------------------------
-- ۱۱. گزارش‌ها: تعاریف و تاریخچه‌ی اجرا
--------------------------------------------------------------------------
DECLARE @rdAnalytics  UNIQUEIDENTIFIER = 'BBBB0000-0000-0000-0000-000000000001';
DECLARE @rdDashboard  UNIQUEIDENTIFIER = 'BBBB0000-0000-0000-0000-000000000002';
DECLARE @rdBenchmark  UNIQUEIDENTIFIER = 'BBBB0000-0000-0000-0000-000000000003';

INSERT INTO report_definitions
  (Id, Name, Description, Type, Format, Schedule, Status,
   SurveyId, SurveyCode, SurveyTitle,
   [From], [To], OrgUnitId, OrgUnitPath, IncludeDescendants,
   OwnerUserId, OwnerUserName, LastExecutedAt, NextRunAt, RetentionCount,
   CreatedAt, IsDeleted)
VALUES
  (@rdAnalytics, N'گزارش تحلیلی نظرسنجی سالانه', N'تحلیل کامل شاخص‌های NPS و CSAT نظرسنجی سالانه',
   1, 2, 2, 1,
   @svAnnual, N'S-1404-ANNUAL', N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴',
   DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, 0, SYSUTCDATETIME()), NULL, NULL, 1,
   @usrAdmin, N'مدیر سامانه', DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, 6, SYSUTCDATETIME()), 10,
   DATEADD(DAY, -13, SYSUTCDATETIME()), 0),
  (@rdDashboard, N'گزارش خلاصه‌ی داشبورد', N'خلاصه‌ی شاخص‌های کلیدی برای مدیریت',
   2, 1, 1, 1,
   NULL, NULL, NULL,
   DATEADD(DAY, -30, SYSUTCDATETIME()), DATEADD(DAY, 0, SYSUTCDATETIME()), NULL, NULL, 1,
   @usrAdmin, N'مدیر سامانه', DATEADD(HOUR, -6, SYSUTCDATETIME()), DATEADD(DAY, 1, SYSUTCDATETIME()), 5,
   DATEADD(DAY, -12, SYSUTCDATETIME()), 0),
  (@rdBenchmark, N'گزارش مقایسه‌ی بنچمارک', N'مقایسه‌ی شاخص‌ها با اهداف سازمانی',
   3, 2, 0, 0,
   @svAnnual, N'S-1404-ANNUAL', N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴',
   DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, 0, SYSUTCDATETIME()), NULL, NULL, 1,
   @usrManager, N'رضا کریمی', NULL, NULL, 3,
   DATEADD(DAY, -5, SYSUTCDATETIME()), 0);

INSERT INTO report_executions
  (Id, ReportDefinitionId, ReportName, ReportType, Format, Status,
   QueuedAt, StartedAt, CompletedAt, TriggeredBy, TriggeredByName,
   FileName, FilePath, FileSizeBytes, [RowCount], ErrorMessage,
   CreatedAt, IsDeleted)
VALUES
  (NEWID(), @rdAnalytics, N'گزارش تحلیلی نظرسنجی سالانه', 1, 2, 2,
   DATEADD(DAY, -2, SYSUTCDATETIME()), DATEADD(DAY, -2, SYSUTCDATETIME()), DATEADD(DAY, -2, SYSUTCDATETIME()),
   @usrAdmin, N'مدیر سامانه',
   N'گزارش-تحلیلی-نظرسنجی-سالانه-۱۴۰۴.xlsx', N'App_Data/reports/annual-analytics.xlsx', 245760, 6, NULL,
   DATEADD(DAY, -2, SYSUTCDATETIME()), 0),
  (NEWID(), @rdAnalytics, N'گزارش تحلیلی نظرسنجی سالانه', 1, 1, 3,
   DATEADD(DAY, -4, SYSUTCDATETIME()), DATEADD(DAY, -4, SYSUTCDATETIME()), DATEADD(DAY, -4, SYSUTCDATETIME()),
   @usrAdmin, N'مدیر سامانه',
   NULL, NULL, NULL, NULL, N'خطا در تولید فایل PDF: فونت فارسی یافت نشد',
   DATEADD(DAY, -4, SYSUTCDATETIME()), 0),
  (NEWID(), @rdDashboard, N'گزارش خلاصه‌ی داشبورد', 2, 1, 0,
   DATEADD(HOUR, -6, SYSUTCDATETIME()), NULL, NULL,
   @usrAdmin, N'مدیر سامانه',
   NULL, NULL, NULL, NULL, NULL,
   DATEADD(HOUR, -6, SYSUTCDATETIME()), 0);

--------------------------------------------------------------------------
-- ۱۲. برنامه‌های اقدام و آیتم‌های اقدام
--------------------------------------------------------------------------
DECLARE @ap1 UNIQUEIDENTIFIER = 'CCCC0000-0000-0000-0000-000000000001';
DECLARE @ap2 UNIQUEIDENTIFIER = 'CCCC0000-0000-0000-0000-000000000002';
DECLARE @ap3 UNIQUEIDENTIFIER = 'CCCC0000-0000-0000-0000-000000000003';

INSERT INTO action_plans
  (Id, Title, Description, Source, SourceKey, SurveyId, SurveyCode, SurveyTitle, CampaignId,
   TriggerMetricType, TriggerMetricValue, OutcomeMetricValue, OutcomeMeasuredAt,
   OrgUnitId, OrgUnitPath, OwnerUserId, OwnerUserName, Priority, Status, DueDate,
   CreatedByUserId, CreatedByUserName, CompletedAt, CreatedAt, IsDeleted)
VALUES
  (@ap1, N'برنامه بهبود رضایت کارکنان واحد فروش', N'اقدامات پیرو نتایج نظرسنجی سالانه برای کاهش نارضایتی در حوزه‌ی حقوق و ابزارها',
   2, N'survey-finding-sales', @svAnnual, N'S-1404-ANNUAL', N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴', @cmEmail,
   1, -50.00, NULL, NULL,
   @orgSal, N'/HQ/SAL', @usrManager, N'رضا کریمی', 4, 1, DATEADD(DAY, 21, SYSUTCDATETIME()),
   @usrAdmin, N'مدیر سامانه', NULL, DATEADD(DAY, -3, SYSUTCDATETIME()), 0),
  (@ap2, N'برنامه آموزش و توسعه‌ی منابع انسانی', N'اقدامات آموزشی بر اساس نیازهای شناسایی‌شده',
   1, N'analytics-alert-training', @svAnnual, N'S-1404-ANNUAL', N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴', NULL,
   3, 3.83, 4.20, DATEADD(DAY, -1, SYSUTCDATETIME()),
   @orgHr, N'/HQ/HR', @usrHr, N'مریم حسینی', 3, 2, DATEADD(DAY, -1, SYSUTCDATETIME()),
   @usrAdmin, N'مدیر سامانه', DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, -40, SYSUTCDATETIME()), 0),
  (@ap3, N'ارتقای زیرساخت فناوری اطلاعات', N'برنامه جایگزینی زیرساخت شبکه و سرورها',
   0, NULL, NULL, NULL, NULL, NULL,
   NULL, NULL, NULL, NULL,
   @orgIt, N'/HQ/IT', @usrManager, N'رضا کریمی', 2, 0, DATEADD(DAY, 45, SYSUTCDATETIME()),
   @usrManager, N'رضا کریمی', NULL, DATEADD(DAY, -1, SYSUTCDATETIME()), 0);

DECLARE @ai1 UNIQUEIDENTIFIER = 'CCCC0000-0000-0000-0000-000000000010';
DECLARE @ai2 UNIQUEIDENTIFIER = 'CCCC0000-0000-0000-0000-000000000011';
DECLARE @ai3 UNIQUEIDENTIFIER = 'CCCC0000-0000-0000-0000-000000000012';
DECLARE @ai4 UNIQUEIDENTIFIER = 'CCCC0000-0000-0000-0000-000000000013';
DECLARE @ai5 UNIQUEIDENTIFIER = 'CCCC0000-0000-0000-0000-000000000014';
DECLARE @ai6 UNIQUEIDENTIFIER = 'CCCC0000-0000-0000-0000-000000000015';

INSERT INTO action_items
  (Id, ActionPlanId, Title, Description, AssigneeUserId, AssigneeUserName,
   Priority, Status, DisplayOrder, DueDate, RemindAt,
   EscalationLevel, EscalatedAt, StartedAt, CompletedAt,
   Effectiveness, EffectivenessNote, EffectivenessAssessedAt, EffectivenessAssessedByUserId,
   CreatedAt, IsDeleted)
VALUES
  (@ai1, @ap1, N'بازنگ ساختار حقوق و دستمزد واحد فروش', N'مطالعه‌ی تطبیقی و پیشنهاد ساختار جدید',
   @usrManager, N'رضا کریمی', 4, 2, 1, DATEADD(DAY, 14, SYSUTCDATETIME()), DATEADD(DAY, 12, SYSUTCDATETIME()),
   0, NULL, DATEADD(DAY, -1, SYSUTCDATETIME()), NULL, 0, NULL, NULL, NULL,
   DATEADD(DAY, -3, SYSUTCDATETIME()), 0),
  (@ai2, @ap1, N'تأمین ابزارهای گزارش‌گیری فروش', N'خرید لایسنس ابزارهای تحلیلی برای تیم فروش',
   @usrAdmin, N'مدیر سامانه', 3, 1, 2, DATEADD(DAY, 7, SYSUTCDATETIME()), DATEADD(DAY, 5, SYSUTCDATETIME()),
   1, DATEADD(DAY, -1, SYSUTCDATETIME()), NULL, NULL, 0, NULL, NULL, NULL,
   DATEADD(DAY, -3, SYSUTCDATETIME()), 0),
  (@ai3, @ap1, N'جلسه‌ی بازخورد با کارکنان فروش', N'ارائه‌ی نتایج نظرسنجی و شنیدن پیشنهادها',
   @usrManager, N'رضا کریمی', 2, 3, 3, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, -3, SYSUTCDATETIME()),
   0, NULL, DATEADD(DAY, -5, SYSUTCDATETIME()), DATEADD(DAY, -1, SYSUTCDATETIME()),
   1, N'کارکنان بازخورد مثبتی درباره‌ی جلسه دادند', DATEADD(DAY, -1, SYSUTCDATETIME()), @usrAdmin,
   DATEADD(DAY, -6, SYSUTCDATETIME()), 0),
  (@ai4, @ap2, N'برگزاری دوره‌ی آموزش ایمنی کار', N'دوره‌ی آموزشی برای کارکنان عملیات',
   @usrHr, N'مریم حسینی', 3, 3, 1, DATEADD(DAY, -2, SYSUTCDATETIME()), DATEADD(DAY, -4, SYSUTCDATETIME()),
   0, NULL, DATEADD(DAY, -10, SYSUTCDATETIME()), DATEADD(DAY, -2, SYSUTCDATETIME()),
   2, N'دوره برگزار شد اما ارزیابی کامل نشده', DATEADD(DAY, -1, SYSUTCDATETIME()), @usrAdmin,
   DATEADD(DAY, -11, SYSUTCDATETIME()), 0),
  (@ai5, @ap2, N'تدوین برنامه‌ی آموزش سالانه', N'برنامه‌ریزی آموزش‌های فنی و نرم',
   @usrHr, N'مریم حسینی', 2, 3, 2, DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, -3, SYSUTCDATETIME()),
   0, NULL, DATEADD(DAY, -20, SYSUTCDATETIME()), DATEADD(DAY, -1, SYSUTCDATETIME()),
   1, N'برنامه تدوین و ابلاغ شد', DATEADD(DAY, -1, SYSUTCDATETIME()), @usrAdmin,
   DATEADD(DAY, -21, SYSUTCDATETIME()), 0),
  (@ai6, @ap3, N'خرید سرورهای جدید', N'جایگزینی سرورهای قدومی',
   @usrManager, N'رضا کریمی', 2, 1, 1, DATEADD(DAY, 30, SYSUTCDATETIME()), DATEADD(DAY, 27, SYSUTCDATETIME()),
   0, NULL, NULL, NULL, 0, NULL, NULL, NULL,
   DATEADD(DAY, -1, SYSUTCDATETIME()), 0);

-- نظرات و پیوست‌ها
INSERT INTO action_comments (Id, ActionItemId, AuthorUserId, AuthorUserName, Body, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @ai1, @usrAdmin,   N'مدیر سامانه', N'لطفاً گزارش تطبیقی را تا پایان هفته ارسال کنید.', DATEADD(DAY, -2, SYSUTCDATETIME()), 0),
  (NEWID(), @ai1, @usrManager, N'رضا کریمی',   N'گزارش اولیه آماده است و در حال بازبینی است.',     DATEADD(DAY, -1, SYSUTCDATETIME()), 0),
  (NEWID(), @ai3, @usrManager, N'رضا کریمی',   N'جلسه با مشارکت خوب کارکنان برگزار شد.',            DATEADD(DAY, -1, SYSUTCDATETIME()), 0);

INSERT INTO action_evidence
  (Id, ActionItemId, FileName, ContentType, FileSizeBytes, StoragePath,
   UploadedByUserId, UploadedByUserName, UploadedAt, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @ai1, N'مطالعه-تطبیقی-حقوق.pdf', N'application/pdf', 524288,
   N'App_Data/files/salary-benchmark-study.pdf', @usrManager, N'رضا کریمی',
   DATEADD(DAY, -2, SYSUTCDATETIME()), DATEADD(DAY, -2, SYSUTCDATETIME()), 0),
  (NEWID(), @ai3, N'صورتجلسه-بازخورد.docx', N'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 81920,
   N'App_Data/files/feedback-meeting-minutes.docx', @usrManager, N'رضا کریمی',
   DATEADD(DAY, -1, SYSUTCDATETIME()), DATEADD(DAY, -1, SYSUTCDATETIME()), 0);

--------------------------------------------------------------------------
-- ۱۳. گردش کار: تعاریف، وضعیت‌ها، گذارها، نمونه‌ها و درخواست‌های تأیید
--------------------------------------------------------------------------
DECLARE @wfSurvey   UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000001';
DECLARE @wfCampaign UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000002';
DECLARE @wfAction   UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000003';

INSERT INTO workflows
  (Id, Name, Code, Description, EntityType, Status, Version,
   CreatedByUserId, CreatedByUserName, CreatedAt, IsDeleted)
VALUES
  (@wfSurvey,   N'چرخه‌ی عمر نظرسنجی', N'WF-SURVEY-LIFECYCLE', N'گردش کار انتشار، فعال‌سازی و بستن نظرسنجی', 0, 1, 1,
   @usrAdmin, N'مدیر سامانه', DATEADD(DAY, -20, SYSUTCDATETIME()), 0),
  (@wfCampaign, N'چرخه‌ی عمر کمپین',  N'WF-CAMPAIGN-LIFECYCLE', N'گردش کار زمان‌بندی، اجرا و تکمیل کمپین', 1, 1, 1,
   @usrAdmin, N'مدیر سامانه', DATEADD(DAY, -20, SYSUTCDATETIME()), 0),
  (@wfAction,   N'چرخه‌ی عمر برنامه‌ی اقدام', N'WF-ACTION-LIFECYCLE', N'گردش کار تصویب و اجرای برنامه‌ی اقدام', 2, 0, 1,
   @usrAdmin, N'مدیر سامانه', DATEADD(DAY, -19, SYSUTCDATETIME()), 0);

-- وضعیت‌ها
DECLARE @wsDraft     UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000010';
DECLARE @wsPublished UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000011';
DECLARE @wsActive    UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000012';
DECLARE @wsClosed    UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000013';
DECLARE @wsCmDraft   UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000020';
DECLARE @wsCmSched   UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000021';
DECLARE @wsCmRunning UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000022';
DECLARE @wsCmDone    UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000023';
DECLARE @wsApDraft   UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000030';
DECLARE @wsApActive  UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000031';
DECLARE @wsApDone    UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000032';

INSERT INTO workflow_states (Id, WorkflowId, Code, Name, IsInitial, IsFinal, DisplayOrder, CreatedAt, IsDeleted)
VALUES
  (@wsDraft,     @wfSurvey,   N'DRAFT',     N'پیش‌نویس',        1, 0, 1, SYSUTCDATETIME(), 0),
  (@wsPublished, @wfSurvey,   N'PUBLISHED', N'منتشرشده',        0, 0, 2, SYSUTCDATETIME(), 0),
  (@wsActive,    @wfSurvey,   N'ACTIVE',    N'فعال',            0, 0, 3, SYSUTCDATETIME(), 0),
  (@wsClosed,    @wfSurvey,   N'CLOSED',    N'بسته‌شده',         0, 1, 4, SYSUTCDATETIME(), 0),
  (@wsCmDraft,   @wfCampaign, N'DRAFT',     N'پیش‌نویس',        1, 0, 1, SYSUTCDATETIME(), 0),
  (@wsCmSched,   @wfCampaign, N'SCHEDULED', N'زمان‌بندی‌شده',    0, 0, 2, SYSUTCDATETIME(), 0),
  (@wsCmRunning, @wfCampaign, N'RUNNING',   N'در حال اجرا',    0, 0, 3, SYSUTCDATETIME(), 0),
  (@wsCmDone,    @wfCampaign, N'COMPLETED', N'تکمیل‌شده',        0, 1, 4, SYSUTCDATETIME(), 0),
  (@wsApDraft,   @wfAction,   N'DRAFT',     N'پیش‌نویس',        1, 0, 1, SYSUTCDATETIME(), 0),
  (@wsApActive,  @wfAction,   N'ACTIVE',    N'فعال',            0, 0, 2, SYSUTCDATETIME(), 0),
  (@wsApDone,    @wfAction,   N'COMPLETED', N'تکمیل‌شده',        0, 1, 3, SYSUTCDATETIME(), 0);

-- گذارها
INSERT INTO workflow_transitions
  (Id, WorkflowId, Code, Name, FromStateId, ToStateId, RequiresApproval,
   ApproverPermission, DisplayOrder, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @wfSurvey,   N'PUBLISH',  N'انتشار',          @wsDraft,     @wsPublished, 1, N'workflows.approve', 1, SYSUTCDATETIME(), 0),
  (NEWID(), @wfSurvey,   N'ACTIVATE', N'فعال‌سازی',        @wsPublished, @wsActive,    0, NULL,                  2, SYSUTCDATETIME(), 0),
  (NEWID(), @wfSurvey,   N'CLOSE',    N'بستن',            @wsActive,    @wsClosed,    1, N'workflows.approve', 3, SYSUTCDATETIME(), 0),
  (NEWID(), @wfCampaign, N'SCHEDULE', N'زمان‌بندی',        @wsCmDraft,   @wsCmSched,   1, N'workflows.approve', 1, SYSUTCDATETIME(), 0),
  (NEWID(), @wfCampaign, N'LAUNCH',   N'اجرا',            @wsCmSched,   @wsCmRunning, 0, NULL,                  2, SYSUTCDATETIME(), 0),
  (NEWID(), @wfCampaign, N'COMPLETE', N'تکمیل',           @wsCmRunning, @wsCmDone,    0, NULL,                  3, SYSUTCDATETIME(), 0),
  (NEWID(), @wfAction,   N'ACTIVATE', N'فعال‌سازی برنامه', @wsApDraft,   @wsApActive,  0, NULL,                  1, SYSUTCDATETIME(), 0),
  (NEWID(), @wfAction,   N'FINISH',   N'تکمیل برنامه',    @wsApActive,  @wsApDone,    0, NULL,                  2, SYSUTCDATETIME(), 0);

-- نمونه‌های گردش کار
DECLARE @wi1 UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000040';
DECLARE @wi2 UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000041';
DECLARE @wi3 UNIQUEIDENTIFIER = 'DDDD0000-0000-0000-0000-000000000042';

INSERT INTO workflow_instances
  (Id, WorkflowId, WorkflowCode, WorkflowVersion, EntityType, EntityId, CurrentStateCode,
   Status, StartedAt, CompletedAt, CancelledAt, StartedByUserId, StartedByUserName,
   ContextJson, TransitionCount, CreatedAt, IsDeleted, OrgUnitId, OrgUnitPath)
VALUES
  (@wi1, @wfSurvey,   N'WF-SURVEY-LIFECYCLE',   1, 0, @svAnnual, N'ACTIVE',
   0, DATEADD(DAY, -14, SYSUTCDATETIME()), NULL, NULL, @usrAdmin, N'مدیر سامانه',
   N'{"title":"نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴"}', 2, DATEADD(DAY, -14, SYSUTCDATETIME()), 0, NULL, NULL),
  (@wi2, @wfCampaign, N'WF-CAMPAIGN-LIFECYCLE', 1, 1, @cmEmail, N'RUNNING',
   0, DATEADD(DAY, -13, SYSUTCDATETIME()), NULL, NULL, @usrAdmin, N'مدیر سامانه',
   N'{"title":"کمپین ایمیل نظرسنجی سالانه"}', 2, DATEADD(DAY, -13, SYSUTCDATETIME()), 0, @hq, N'/HQ'),
  (@wi3, @wfCampaign, N'WF-CAMPAIGN-LIFECYCLE', 1, 1, @cmInApp, N'COMPLETED',
   1, DATEADD(DAY, -9, SYSUTCDATETIME()), DATEADD(DAY, -2, SYSUTCDATETIME()), NULL, @usrAdmin, N'مدیر سامانه',
   N'{"title":"اعلان درون‌برنامه‌ای نظرسنجی ابزارها"}', 3, DATEADD(DAY, -9, SYSUTCDATETIME()), 0, @hq, N'/HQ');

-- درخواست‌های تأیید
INSERT INTO workflow_approval_requests
  (Id, InstanceId, TransitionId, TransitionCode, FromStateCode, ToStateCode,
   ApproverPermission, Status, RequestedAt, RequestedById,
   DecidedAt, DecidedById, DecidedByUserName, DecisionNote, ExpiresAt,
   CreatedAt, IsDeleted)
VALUES
  (NEWID(), @wi1, (SELECT TOP 1 Id FROM workflow_transitions WHERE WorkflowId = @wfSurvey AND Code = N'CLOSE'),
   N'CLOSE', N'ACTIVE', N'CLOSED', N'workflows.approve', 0,
   DATEADD(HOUR, -5, SYSUTCDATETIME()), @usrAdmin, NULL, NULL, NULL, NULL, DATEADD(DAY, 7, SYSUTCDATETIME()),
   DATEADD(HOUR, -5, SYSUTCDATETIME()), 0),
  (NEWID(), @wi2, (SELECT TOP 1 Id FROM workflow_transitions WHERE WorkflowId = @wfCampaign AND Code = N'SCHEDULE'),
   N'SCHEDULE', N'DRAFT', N'SCHEDULED', N'workflows.approve', 1,
   DATEADD(DAY, -13, SYSUTCDATETIME()), @usrAdmin,
   DATEADD(DAY, -13, SYSUTCDATETIME()), @usrAdmin, N'مدیر سامانه', N'تأیید شد', NULL,
   DATEADD(DAY, -13, SYSUTCDATETIME()), 0);

--------------------------------------------------------------------------
-- ۱۴. یکپارچه‌سازی: اندپوینت‌ها و تحویل وب‌هوک
--------------------------------------------------------------------------
DECLARE @ieWebhook  UNIQUEIDENTIFIER = 'EEEE0000-0000-0000-0000-000000000001';
DECLARE @ieInbound  UNIQUEIDENTIFIER = 'EEEE0000-0000-0000-0000-000000000002';
DECLARE @ieHrSync   UNIQUEIDENTIFIER = 'EEEE0000-0000-0000-0000-000000000003';
DECLARE @ieAi       UNIQUEIDENTIFIER = 'EEEE0000-0000-0000-0000-000000000004';

INSERT INTO integration_endpoints
  (Id, Name, Code, Description, Type, Url, HttpMethod, AuthType, SecretRef, AuthHeaderName,
   IsActive, TimeoutSeconds, MaxRetries, SubscribedEvents,
   CreatedByUserId, CreatedByUserName, SuccessfulDeliveries, LastDeliveryError, LastDeliveryAt,
   CreatedAt, IsDeleted)
VALUES
  (@ieWebhook, N'وب‌هوک خروجی سامانه‌ی تحلیل ها', N'WH-ANALYTICS-OUT',
   N'ارسال رویدادهای تحلیلی به سامانه‌ی داشبورد سازمانی', 0,
   N'https://analytics.odcc.local/api/webhooks/survey-events', N'POST', 1,
   N'odcc:analytics-outbound', N'X-ODCC-Signature',
   1, 30, 3, N'survey.submitted;campaign.completed;report.generated',
   @usrAdmin, N'مدیر سامانه', 18, NULL, DATEADD(HOUR, -2, SYSUTCDATETIME()),
   DATEADD(DAY, -15, SYSUTCDATETIME()), 0),
  (@ieInbound, N'وب‌هوک ورودی همگون‌سازی کارکنان', N'WH-HR-INBOUND',
   N'دریافت به‌روزرسانی‌های کارکنان از سامانه‌ی منابع انسانی', 1,
   N'/api/webhooks/wh-hr-inbound', N'POST', 1,
   N'odcc:hr-inbound', N'X-ODCC-Signature',
   1, 30, 3, N'hr.employee.updated;hr.employee.created',
   @usrAdmin, N'مدیر سامانه', 6, NULL, DATEADD(DAY, -1, SYSUTCDATETIME()),
   DATEADD(DAY, -15, SYSUTCDATETIME()), 0),
  (@ieHrSync, N'همگون‌سازی منابع انسانی', N'HR-SYNC',
   N'همگون‌سازی داده‌های کارکنان از سامانه‌ی منابع انسانی', 2,
   N'https://hr.odcc.local/api/employees/sync', N'POST', 2,
   N'odcc:hr-sync-token', N'Authorization',
   0, 60, 2, N'hr.sync.daily',
   @usrAdmin, N'مدیر سامانه', 0, N'تایم‌اوت اتصال به سامانه‌ی منابع انسانی', DATEADD(DAY, -3, SYSUTCDATETIME()),
   DATEADD(DAY, -12, SYSUTCDATETIME()), 0),
  (@ieAi, N'ارائه‌دهنده‌ی تحلیل هوش مصنوعی', N'AI-ANALYST',
   N'تحلیل خودکار پاسخ‌های متنی نظرسنجی‌ها', 4,
   N'https://ai.odcc.local/api/sentiment', N'POST', 3,
   N'odcc:ai-provider-key', N'X-API-Key',
   1, 45, 2, N'response.submitted',
   @usrAdmin, N'مدیر سامانه', 4, NULL, DATEADD(HOUR, -8, SYSUTCDATETIME()),
   DATEADD(DAY, -8, SYSUTCDATETIME()), 0);

INSERT INTO webhook_deliveries
  (Id, EndpointId, EndpointCode, EventType, EventId, PayloadJson, Status, AttemptCount,
   LastAttemptAt, NextAttemptAt, DeliveredAt, ResponseStatusCode, LastError, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @ieWebhook, N'WH-ANALYTICS-OUT', N'survey.submitted', N'evt-2026-0001',
   N'{"surveyId":"88888888-0000-0000-0000-000000000001","surveyCode":"S-1404-ANNUAL","sessionId":"AAAA0000-0000-0000-0000-000000000001"}',
   1, 1, DATEADD(HOUR, -10, SYSUTCDATETIME()), NULL, DATEADD(HOUR, -10, SYSUTCDATETIME()), 200, NULL,
   DATEADD(HOUR, -10, SYSUTCDATETIME()), 0),
  (NEWID(), @ieWebhook, N'WH-ANALYTICS-OUT', N'survey.submitted', N'evt-2026-0002',
   N'{"surveyId":"88888888-0000-0000-0000-000000000001","surveyCode":"S-1404-ANNUAL","sessionId":"AAAA0000-0000-0000-0000-000000000002"}',
   1, 1, DATEADD(HOUR, -9, SYSUTCDATETIME()), NULL, DATEADD(HOUR, -9, SYSUTCDATETIME()), 200, NULL,
   DATEADD(HOUR, -9, SYSUTCDATETIME()), 0),
  (NEWID(), @ieWebhook, N'WH-ANALYTICS-OUT', N'campaign.completed', N'evt-2026-0003',
   N'{"campaignId":"99999999-0000-0000-0000-000000000003","campaignCode":"C-1404-INAPP"}',
   2, 3, DATEADD(HOUR, -4, SYSUTCDATETIME()), DATEADD(HOUR, -2, SYSUTCDATETIME()), NULL, 503,
   N'Service Unavailable — سرور مقصد در دسترس نیست',
   DATEADD(HOUR, -4, SYSUTCDATETIME()), 0),
  (NEWID(), @ieWebhook, N'WH-ANALYTICS-OUT', N'report.generated', N'evt-2026-0004',
   N'{"reportDefinitionId":"BBBB0000-0000-0000-0000-000000000001","format":"Excel"}',
   1, 1, DATEADD(HOUR, -2, SYSUTCDATETIME()), NULL, DATEADD(HOUR, -2, SYSUTCDATETIME()), 200, NULL,
   DATEADD(HOUR, -2, SYSUTCDATETIME()), 0),
  (NEWID(), @ieInbound, N'WH-HR-INBOUND', N'hr.employee.updated', N'evt-hr-0001',
   N'{"employeeCode":"EMP-0002","firstName":"رضا","lastName":"کریمی"}',
   1, 1, DATEADD(DAY, -1, SYSUTCDATETIME()), NULL, DATEADD(DAY, -1, SYSUTCDATETIME()), 202, NULL,
   DATEADD(DAY, -1, SYSUTCDATETIME()), 0),
  (NEWID(), @ieWebhook, N'WH-ANALYTICS-OUT', N'survey.submitted', N'evt-2026-0005',
   N'{"surveyId":"88888888-0000-0000-0000-000000000003","surveyCode":"S-1404-TOOLS"}',
   0, 0, NULL, DATEADD(MINUTE, -30, SYSUTCDATETIME()), NULL, NULL, NULL,
   DATEADD(MINUTE, -30, SYSUTCDATETIME()), 0);

-- به‌روزرسانی شمارنده‌ی تحویل موفق اندپوینت
UPDATE integration_endpoints
SET SuccessfulDeliveries = 3, LastDeliveryAt = DATEADD(HOUR, -2, SYSUTCDATETIME())
WHERE Code = N'WH-ANALYTICS-OUT';

--------------------------------------------------------------------------
-- ۱۵. اعلان‌ها و قالب‌های اعلان
--------------------------------------------------------------------------
DECLARE @ntTplCampaign UNIQUEIDENTIFIER = 'FFFF0000-0000-0000-0000-000000000001';
DECLARE @ntTplWorkflow UNIQUEIDENTIFIER = 'FFFF0000-0000-0000-0000-000000000002';

INSERT INTO notification_templates
  (Id, Code, Name, Channel, Category, IsActive, CreatedAt, IsDeleted)
VALUES
  (@ntTplCampaign, N'NT-CAMPAIGN-INVITE', N'دعوت به نظرسنجی',   2, 1, 1, DATEADD(DAY, -20, SYSUTCDATETIME()), 0),
  (@ntTplWorkflow, N'NT-WORKFLOW-APPROVAL', N'درخواست تأیید',   1, 6, 1, DATEADD(DAY, -20, SYSUTCDATETIME()), 0);

INSERT INTO notifications
  (Id, RecipientUserId, RecipientName, RecipientEmail, RecipientPhone,
   Channel, Category, Status, TemplateCode, Subject, Body, Language,
   RetryCount, MaxRetries, NextTryAt, SentAt, DeliveredAt, ReadAt, LastError,
   ProviderMessageId, SourceType, SourceId, Url, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @usrAdmin, N'مدیر سامانه', N'admin@odcc.local', NULL,
   2, 1, 3, N'NT-CAMPAIGN-INVITE',
   N'دعوت به نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴',
   N'کاربر گرامی، شما به نظرسنجی سالانه دعوت شده‌اید. لطفاً از طریق پنل کاربری پاسخ دهید.',
   1, 0, 3, NULL, DATEADD(DAY, -13, SYSUTCDATETIME()), DATEADD(DAY, -13, SYSUTCDATETIME()), NULL, NULL,
   NULL, N'Campaign', '99999999-0000-0000-0000-000000000001', N'/fa/responses/my-surveys',
   DATEADD(DAY, -13, SYSUTCDATETIME()), 0),
  (NEWID(), @usrManager, N'رضا کریمی', N'manager.it@odcc.local', NULL,
   2, 6, 3, N'NT-WORKFLOW-APPROVAL',
   N'درخواست تأیید: بستن نظرسنجی سالانه',
   N'درخواست تأیید گذار «بستن» برای نظرسنجی سالانه ثبت شده است. لطفاً تصمیم‌گیری کنید.',
   1, 0, 3, NULL, DATEADD(HOUR, -5, SYSUTCDATETIME()), DATEADD(HOUR, -5, SYSUTCDATETIME()), NULL, NULL,
   NULL, N'WorkflowApproval', NULL, N'/fa/workflow-approvals',
   DATEADD(HOUR, -5, SYSUTCDATETIME()), 0),
  (NEWID(), @usrAdmin, N'مدیر سامانه', NULL, NULL,
   1, 6, 1, N'NT-WORKFLOW-APPROVAL',
   N'یادآور: درخواست تأیید در انتظار',
   N'یک درخواست تأیید جدید در انتظار تصمیم شماست.',
   1, 0, 3, DATEADD(HOUR, 1, SYSUTCDATETIME()), NULL, NULL, NULL, NULL,
   NULL, NULL, NULL, N'/fa/workflow-approvals',
   DATEADD(MINUTE, -10, SYSUTCDATETIME()), 0);

COMMIT TRANSACTION;
PRINT N'بخش سوم داده‌ها (گزارش، اقدام، گردش کار، یکپارچه‌سازی، اعلان) درج شد.';
