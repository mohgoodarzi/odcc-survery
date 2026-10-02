/* ============================================================================
 * ODCC Survey — مجموعه‌داده‌ی نمونه‌ی فارسی — بخش دوم
 * نشست‌های پاسخ‌گویی، پاسخ‌ها، تحلیلات، گزارش‌ها، اقدامات، گردش کار،
 * یکپارچه‌سازی‌ها و اعلان‌ها
 * ========================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @svAnnual   UNIQUEIDENTIFIER = '88888888-0000-0000-0000-000000000001';
DECLARE @svWorkpl   UNIQUEIDENTIFIER = '88888888-0000-0000-0000-000000000002';
DECLARE @svTools    UNIQUEIDENTIFIER = '88888888-0000-0000-0000-000000000003';
DECLARE @cmEmail    UNIQUEIDENTIFIER = '99999999-0000-0000-0000-000000000001';
DECLARE @usrAdmin   UNIQUEIDENTIFIER = 'F1C472EE-C4AA-4EB2-0CD1-08DF1A521574';
DECLARE @usrManager UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000001';
DECLARE @usrHr      UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000002';
DECLARE @usrEmp     UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000003';
DECLARE @empAdmin   UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000001';
DECLARE @empManager UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000002';
DECLARE @empHr      UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000003';
DECLARE @empSales1  UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000004';
DECLARE @empSales2  UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000005';
DECLARE @empOps1    UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000006';

DECLARE @itNps     UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000020';
DECLARE @itCsat    UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000021';
DECLARE @itEtype   UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000022';
DECLARE @itImprove UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000023';
DECLARE @itTools   UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000024';
DECLARE @itComment UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000025';

DECLARE @qNps     UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000001';
DECLARE @qCsat    UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000002';
DECLARE @qEtype   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000003';
DECLARE @qImprove UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000004';
DECLARE @qTools   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000005';
DECLARE @qComment UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000006';

DECLARE @oEtypeFull    UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000001';
DECLARE @oEtypePart    UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000002';
DECLARE @oEtypeCon     UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000003';
DECLARE @oImproveSal   UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000004';
DECLARE @oImproveEnv   UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000005';
DECLARE @oImproveTool  UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000006';
DECLARE @oImproveTrain UNIQUEIDENTIFIER = '66666666-0000-0000-0000-000000000008';

--------------------------------------------------------------------------
-- ۹. نشست‌های پاسخ‌گویی و پاسخ‌ها (به پرسشنامه و کمپین متصل)
-- NPS: ۹ و ۱۰ = Promoter، ۷ و ۸ = Passive، ۰ تا ۶ = Detractor
--------------------------------------------------------------------------
DECLARE @rs1 UNIQUEIDENTIFIER = 'AAAA0000-0000-0000-0000-000000000001';
DECLARE @rs2 UNIQUEIDENTIFIER = 'AAAA0000-0000-0000-0000-000000000002';
DECLARE @rs3 UNIQUEIDENTIFIER = 'AAAA0000-0000-0000-0000-000000000003';
DECLARE @rs4 UNIQUEIDENTIFIER = 'AAAA0000-0000-0000-0000-000000000004';
DECLARE @rs5 UNIQUEIDENTIFIER = 'AAAA0000-0000-0000-0000-000000000005';
DECLARE @rs6 UNIQUEIDENTIFIER = 'AAAA0000-0000-0000-0000-000000000006';
DECLARE @rs7 UNIQUEIDENTIFIER = 'AAAA0000-0000-0000-0000-000000000007';
DECLARE @rs8 UNIQUEIDENTIFIER = 'AAAA0000-0000-0000-0000-000000000008';
DECLARE @rs9 UNIQUEIDENTIFIER = 'AAAA0000-0000-0000-0000-000000000009';

INSERT INTO response_sessions
  (Id, SurveyId, SurveyCode, CampaignId, CampaignCode, RespondentUserId, RespondentEmployeeId,
   RespondentDisplayName, IsAnonymous, Status, Source, ResponseLanguage, StartedAt, SubmittedAt,
   LastActivityAt, AnswerCount, CreatedAt, IsDeleted)
VALUES
  (@rs1, @svAnnual, N'S-1404-ANNUAL', @cmEmail, N'C-1404-EMAIL', @usrAdmin,   @empAdmin,
   N'مدیر سامانه', 0, 2, 2, 1,
   DATEADD(DAY, -12, SYSUTCDATETIME()), DATEADD(DAY, -12, SYSUTCDATETIME()),
   DATEADD(DAY, -12, SYSUTCDATETIME()), 6, DATEADD(DAY, -12, SYSUTCDATETIME()), 0),
  (@rs2, @svAnnual, N'S-1404-ANNUAL', @cmEmail, N'C-1404-EMAIL', @usrManager, @empManager,
   N'رضا کریمی', 0, 2, 2, 1,
   DATEADD(DAY, -11, SYSUTCDATETIME()), DATEADD(DAY, -10, SYSUTCDATETIME()),
   DATEADD(DAY, -10, SYSUTCDATETIME()), 6, DATEADD(DAY, -11, SYSUTCDATETIME()), 0),
  (@rs3, @svAnnual, N'S-1404-ANNUAL', @cmEmail, N'C-1404-EMAIL', @usrHr, @empHr,
   N'مریم حسینی', 0, 2, 2, 1,
   DATEADD(DAY, -10, SYSUTCDATETIME()), DATEADD(DAY, -9, SYSUTCDATETIME()),
   DATEADD(DAY, -9, SYSUTCDATETIME()), 6, DATEADD(DAY, -10, SYSUTCDATETIME()), 0),
  (@rs4, @svAnnual, N'S-1404-ANNUAL', @cmEmail, N'C-1404-EMAIL', @usrEmp, @empSales1,
   N'علی محمدی', 0, 2, 2, 1,
   DATEADD(DAY, -8, SYSUTCDATETIME()), DATEADD(DAY, -8, SYSUTCDATETIME()),
   DATEADD(DAY, -8, SYSUTCDATETIME()), 6, DATEADD(DAY, -8, SYSUTCDATETIME()), 0),
  (@rs5, @svAnnual, N'S-1404-ANNUAL', @cmEmail, N'C-1404-EMAIL', NULL, @empSales2,
   N'زهرا احمدی', 0, 2, 2, 1,
   DATEADD(DAY, -7, SYSUTCDATETIME()), DATEADD(DAY, -7, SYSUTCDATETIME()),
   DATEADD(DAY, -7, SYSUTCDATETIME()), 6, DATEADD(DAY, -7, SYSUTCDATETIME()), 0),
  (@rs6, @svAnnual, N'S-1404-ANNUAL', NULL, NULL, NULL, @empOps1,
   N'حسین رضایی', 0, 2, 1, 1,
   DATEADD(DAY, -5, SYSUTCDATETIME()), DATEADD(DAY, -5, SYSUTCDATETIME()),
   DATEADD(DAY, -5, SYSUTCDATETIME()), 6, DATEADD(DAY, -5, SYSUTCDATETIME()), 0),
  (@rs7, @svAnnual, N'S-1404-ANNUAL', @cmEmail, N'C-1404-EMAIL', @usrEmp, @empSales1,
   N'علی محمدی', 0, 1, 2, 1,
   DATEADD(HOUR, -3, SYSUTCDATETIME()), NULL,
   DATEADD(HOUR, -2, SYSUTCDATETIME()), 3, DATEADD(HOUR, -3, SYSUTCDATETIME()), 0),
  (@rs8, @svTools, N'S-1404-TOOLS', NULL, NULL, NULL, NULL,
   NULL, 1, 2, 5, 1,
   DATEADD(DAY, -9, SYSUTCDATETIME()), DATEADD(DAY, -9, SYSUTCDATETIME()),
   DATEADD(DAY, -9, SYSUTCDATETIME()), 4, DATEADD(DAY, -9, SYSUTCDATETIME()), 0),
  (@rs9, @svTools, N'S-1404-TOOLS', NULL, NULL, NULL, NULL,
   NULL, 1, 1, 5, 1,
   DATEADD(HOUR, -1, SYSUTCDATETIME()), NULL,
   DATEADD(MINUTE, -30, SYSUTCDATETIME()), 2, DATEADD(HOUR, -1, SYSUTCDATETIME()), 0);

-- پاسخ‌ها برای نشست‌های ارسال‌شده (هر نشست: NPS، CSAT، نوع استخدام، بهبود، ابزار، نظر)
INSERT INTO response_answers
  (Id, SessionId, QuestionnaireItemId, QuestionId, QuestionCode, QuestionType, DisplayOrder,
   TextValue, NumericValue, CreatedAt, IsDeleted)
VALUES
  -- نشست ۱ (مدیر سامانه) — NPS 9، CSAT 5، تمام‌وقت، بهبود: حقوق/آموزش، ابزار بله
  (NEWID(), @rs1, @itNps,     @qNps,     N'Q-NPS',     3, 1, NULL, 9,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs1, @itCsat,    @qCsat,    N'Q-CSAT',    3, 2, NULL, 5,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs1, @itEtype,   @qEtype,   N'Q-ETYPE',   1, 3, N'تمام‌وقت', NULL, SYSUTCDATETIME(), 0),
  (NEWID(), @rs1, @itImprove, @qImprove, N'Q-IMPROVE', 2, 4, NULL, NULL,   SYSUTCDATETIME(), 0),
  (NEWID(), @rs1, @itTools,   @qTools,   N'Q-TOOLS',   4, 5, N'بله', 1,    SYSUTCDATETIME(), 0),
  (NEWID(), @rs1, @itComment, @qComment, N'Q-COMMENT', 6, 6, N'بهبود فرآیند‌های داخلی و سامانه‌های اتوماسیون', NULL, SYSUTCDATETIME(), 0),
  -- نشست ۲ (مدیر فناوری اطلاعات) — NPS 8، CSAT 4
  (NEWID(), @rs2, @itNps,     @qNps,     N'Q-NPS',     3, 1, NULL, 8,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs2, @itCsat,    @qCsat,    N'Q-CSAT',    3, 2, NULL, 4,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs2, @itEtype,   @qEtype,   N'Q-ETYPE',   1, 3, N'تمام‌وقت', NULL, SYSUTCDATETIME(), 0),
  (NEWID(), @rs2, @itImprove, @qImprove, N'Q-IMPROVE', 2, 4, NULL, NULL,   SYSUTCDATETIME(), 0),
  (NEWID(), @rs2, @itTools,   @qTools,   N'Q-TOOLS',   4, 5, N'بله', 1,    SYSUTCDATETIME(), 0),
  (NEWID(), @rs2, @itComment, @qComment, N'Q-COMMENT', 6, 6, N'زیرساخت شبکه نیاز به به‌روزرسانی دارد', NULL, SYSUTCDATETIME(), 0),
  -- نشست ۳ (کارشناس منابع انسانی) — NPS 10، CSAT 5
  (NEWID(), @rs3, @itNps,     @qNps,     N'Q-NPS',     3, 1, NULL, 10,    SYSUTCDATETIME(), 0),
  (NEWID(), @rs3, @itCsat,    @qCsat,    N'Q-CSAT',    3, 2, NULL, 5,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs3, @itEtype,   @qEtype,   N'Q-ETYPE',   1, 3, N'پیمانی',  NULL, SYSUTCDATETIME(), 0),
  (NEWID(), @rs3, @itImprove, @qImprove, N'Q-IMPROVE', 2, 4, NULL, NULL,   SYSUTCDATETIME(), 0),
  (NEWID(), @rs3, @itTools,   @qTools,   N'Q-TOOLS',   4, 5, N'بله', 1,    SYSUTCDATETIME(), 0),
  (NEWID(), @rs3, @itComment, @qComment, N'Q-COMMENT', 6, 6, N'همکاری تیمی بسیار خوب است', NULL, SYSUTCDATETIME(), 0),
  -- نشست ۴ (کارمند فروش) — NPS 6، CSAT 3
  (NEWID(), @rs4, @itNps,     @qNps,     N'Q-NPS',     3, 1, NULL, 6,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs4, @itCsat,    @qCsat,    N'Q-CSAT',    3, 2, NULL, 3,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs4, @itEtype,   @qEtype,   N'Q-ETYPE',   1, 3, N'تمام‌وقت', NULL, SYSUTCDATETIME(), 0),
  (NEWID(), @rs4, @itImprove, @qImprove, N'Q-IMPROVE', 2, 4, NULL, NULL,   SYSUTCDATETIME(), 0),
  (NEWID(), @rs4, @itTools,   @qTools,   N'Q-TOOLS',   4, 5, N'خیر', 0,    SYSUTCDATETIME(), 0),
  (NEWID(), @rs4, @itComment, @qComment, N'Q-COMMENT', 6, 6, N'حقوق و دستمزد نیاز به بازنگری دارد', NULL, SYSUTCDATETIME(), 0),
  -- نشست ۵ (زهرا احمدی) — NPS 7، CSAT 4
  (NEWID(), @rs5, @itNps,     @qNps,     N'Q-NPS',     3, 1, NULL, 7,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs5, @itCsat,    @qCsat,    N'Q-CSAT',    3, 2, NULL, 4,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs5, @itEtype,   @qEtype,   N'Q-ETYPE',   1, 3, N'پاره‌وقت', NULL, SYSUTCDATETIME(), 0),
  (NEWID(), @rs5, @itImprove, @qImprove, N'Q-IMPROVE', 2, 4, NULL, NULL,   SYSUTCDATETIME(), 0),
  (NEWID(), @rs5, @itTools,   @qTools,   N'Q-TOOLS',   4, 5, N'بله', 1,    SYSUTCDATETIME(), 0),
  (NEWID(), @rs5, @itComment, @qComment, N'Q-COMMENT', 6, 6, N'ساعات کاری انعطاف‌پذیر باشد', NULL, SYSUTCDATETIME(), 0),
  -- نشست ۶ (حسین رضایی) — NPS 5، CSAT 2
  (NEWID(), @rs6, @itNps,     @qNps,     N'Q-NPS',     3, 1, NULL, 5,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs6, @itCsat,    @qCsat,    N'Q-CSAT',    3, 2, NULL, 2,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs6, @itEtype,   @qEtype,   N'Q-ETYPE',   1, 3, N'پیمانی',  NULL, SYSUTCDATETIME(), 0),
  (NEWID(), @rs6, @itImprove, @qImprove, N'Q-IMPROVE', 2, 4, NULL, NULL,   SYSUTCDATETIME(), 0),
  (NEWID(), @rs6, @itTools,   @qTools,   N'Q-TOOLS',   4, 5, N'خیر', 0,    SYSUTCDATETIME(), 0),
  (NEWID(), @rs6, @itComment, @qComment, N'Q-COMMENT', 6, 6, N'نیاز به آموزش بیشتر در حوزه‌ی ایمنی داریم', NULL, SYSUTCDATETIME(), 0),
  -- نشست ۷ (در حال انجام — فقط ۳ پاسخ)
  (NEWID(), @rs7, @itNps,     @qNps,     N'Q-NPS',     3, 1, NULL, 9,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs7, @itCsat,    @qCsat,    N'Q-CSAT',    3, 2, NULL, 4,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs7, @itEtype,   @qEtype,   N'Q-ETYPE',   1, 3, N'تمام‌وقت', NULL, SYSUTCDATETIME(), 0),
  -- نشست ۸ (ناشناس، ابزارها) — NPS 8، CSAT 4، ابزارها بله
  (NEWID(), @rs8, @itNps,     @qNps,     N'Q-NPS',     3, 1, NULL, 8,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs8, @itCsat,    @qCsat,    N'Q-CSAT',    3, 2, NULL, 4,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs8, @itTools,   @qTools,   N'Q-TOOLS',   4, 5, N'بله', 1,    SYSUTCDATETIME(), 0),
  (NEWID(), @rs8, @itComment, @qComment, N'Q-COMMENT', 6, 6, N'ابزارهای گزارش‌گیری کارآمد هستند', NULL, SYSUTCDATETIME(), 0),
  -- نشست ۹ (ناشناس، در حال انجام)
  (NEWID(), @rs9, @itNps,     @qNps,     N'Q-NPS',     3, 1, NULL, 7,     SYSUTCDATETIME(), 0),
  (NEWID(), @rs9, @itCsat,    @qCsat,    N'Q-CSAT',    3, 2, NULL, 3,     SYSUTCDATETIME(), 0);

-- انتخاب‌ها برای سؤالات تک‌گزینه‌ای/چندگزینه‌ای
DECLARE @ans1Etype UNIQUEIDENTIFIER;
DECLARE @ans1Impr1 UNIQUEIDENTIFIER;
DECLARE @ans2Etype UNIQUEIDENTIFIER;
DECLARE @ans2Impr1 UNIQUEIDENTIFIER;
DECLARE @ans3Etype UNIQUEIDENTIFIER;
DECLARE @ans3Impr1 UNIQUEIDENTIFIER;
DECLARE @ans4Etype UNIQUEIDENTIFIER;
DECLARE @ans4Impr1 UNIQUEIDENTIFIER;
DECLARE @ans5Etype UNIQUEIDENTIFIER;
DECLARE @ans5Impr1 UNIQUEIDENTIFIER;
DECLARE @ans5Impr2 UNIQUEIDENTIFIER;
DECLARE @ans6Etype UNIQUEIDENTIFIER;
DECLARE @ans6Impr1 UNIQUEIDENTIFIER;

SELECT @ans1Etype = a.Id FROM response_answers a WHERE a.SessionId = @rs1 AND a.QuestionCode = N'Q-ETYPE';
SELECT @ans1Impr1 = a.Id FROM response_answers a WHERE a.SessionId = @rs1 AND a.QuestionCode = N'Q-IMPROVE';
SELECT @ans2Etype = a.Id FROM response_answers a WHERE a.SessionId = @rs2 AND a.QuestionCode = N'Q-ETYPE';
SELECT @ans2Impr1 = a.Id FROM response_answers a WHERE a.SessionId = @rs2 AND a.QuestionCode = N'Q-IMPROVE';
SELECT @ans3Etype = a.Id FROM response_answers a WHERE a.SessionId = @rs3 AND a.QuestionCode = N'Q-ETYPE';
SELECT @ans3Impr1 = a.Id FROM response_answers a WHERE a.SessionId = @rs3 AND a.QuestionCode = N'Q-IMPROVE';
SELECT @ans4Etype = a.Id FROM response_answers a WHERE a.SessionId = @rs4 AND a.QuestionCode = N'Q-ETYPE';
SELECT @ans4Impr1 = a.Id FROM response_answers a WHERE a.SessionId = @rs4 AND a.QuestionCode = N'Q-IMPROVE';
SELECT @ans5Etype = a.Id FROM response_answers a WHERE a.SessionId = @rs5 AND a.QuestionCode = N'Q-ETYPE';
SELECT @ans5Impr1 = a.Id FROM response_answers a WHERE a.SessionId = @rs5 AND a.QuestionCode = N'Q-IMPROVE';
SELECT @ans6Etype = a.Id FROM response_answers a WHERE a.SessionId = @rs6 AND a.QuestionCode = N'Q-ETYPE';
SELECT @ans6Impr1 = a.Id FROM response_answers a WHERE a.SessionId = @rs6 AND a.QuestionCode = N'Q-IMPROVE';

INSERT INTO response_answer_selections (Id, AnswerId, OptionId, OptionCode, DisplayOrder, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @ans1Etype, @oEtypeFull,    N'FULLTIME', 1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans1Impr1, @oImproveSal,   N'SALARY',   1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans1Impr1, @oImproveTrain, N'TRAINING', 2, SYSUTCDATETIME(), 0),
  (NEWID(), @ans2Etype, @oEtypeFull,    N'FULLTIME', 1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans2Impr1, @oImproveTool,  N'TOOLS',    1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans3Etype, @oEtypeCon,     N'CONTRACT', 1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans3Impr1, @oImproveTrain, N'TRAINING', 1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans4Etype, @oEtypeFull,    N'FULLTIME', 1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans4Impr1, @oImproveSal,   N'SALARY',   1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans5Etype, @oEtypePart,    N'PARTTIME', 1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans5Impr1, @oImproveEnv,   N'ENVIRON',  1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans5Impr1, @oImproveSal,   N'SALARY',   2, SYSUTCDATETIME(), 0),
  (NEWID(), @ans6Etype, @oEtypeCon,     N'CONTRACT', 1, SYSUTCDATETIME(), 0),
  (NEWID(), @ans6Impr1, @oImproveTool,  N'TOOLS',    1, SYSUTCDATETIME(), 0);

-- پاسخ‌های DateAdd را برای گزارش/تحلیل روی نشست‌های ارسال‌شده تنظیم می‌کنیم
UPDATE response_sessions SET LastActivityAt = SubmittedAt WHERE Status = 2 AND SubmittedAt IS NOT NULL;

--------------------------------------------------------------------------
-- ۱۰. تحلیلات: شاخص‌های محاسبه‌شده‌ی نظرسنجی (داشبورد از این جدول می‌خواند)
-- NPS سالانه: Promoters=2 (9,10)％， Passives=2 (7,8)％， Detractors=2 (5,6)
-- یعنی NPS = (2/6 - 2/6)*100 = 0 → مقدار 0 برای واقعی بودن: 16.67
--------------------------------------------------------------------------
DECLARE @orgIt  UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000001';
DECLARE @orgHr  UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000002';
DECLARE @orgSal UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000003';
DECLARE @orgOps UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000004';
DECLARE @hq     UNIQUEIDENTIFIER = '01A0D412-C716-7C18-8D85-68187B170E3D';

-- نشست‌های ارسال‌شده‌ی نظرسنجی سالانه به واحدهای مربوطه منتسب شوند
UPDATE response_sessions SET IsDeleted = 0;

INSERT INTO survey_metrics
  (Id, SurveyId, SurveyCode, SurveyTitle, IsAnonymous, SegmentType, OrgUnitId, OrgUnitPath,
   CampaignId, CampaignCode, Source, WindowStart, WindowEnd, TotalSessions, CompletedSessions,
   CompletionRate, NpsScore, NpsPromoters, NpsPassives, NpsDetractors, NpsQuestionId,
   CsatScore, CsatRespondents, CsatQuestionId,
   CesScore, CesRespondents, CesQuestionId,
   AverageRating, RatingRespondents, ResponseRate, TotalDistributions, RespondedDistributions,
   QuestionMetrics, ComputedAt, CreatedAt, IsDeleted)
VALUES
  (NEWID(), @svAnnual, N'S-1404-ANNUAL', N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴', 0,
   1, NULL, NULL, NULL, NULL, NULL,
   DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, 0, SYSUTCDATETIME()),
   7, 6, 85.71,
   0.00, 3, 2, 2, @qNps,
   3.83, 6, @qCsat,
   NULL, 0, NULL,
   3.42, 6, 60.00, 6, 2,
   N'[]', SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
  (NEWID(), @svAnnual, N'S-1404-ANNUAL', N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴', 0,
   3, @orgIt, N'/HQ/IT', NULL, NULL, NULL,
   DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, 0, SYSUTCDATETIME()),
   1, 1, 100.00,
   33.33, 1, 0, 0, @qNps,
   4.00, 1, @qCsat,
   NULL, 0, NULL,
   4.00, 1, NULL, 1, 1,
   N'[]', SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
  (NEWID(), @svAnnual, N'S-1404-ANNUAL', N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴', 0,
   3, @orgHr, N'/HQ/HR', NULL, NULL, NULL,
   DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, 0, SYSUTCDATETIME()),
   1, 1, 100.00,
   100.00, 1, 0, 0, @qNps,
   5.00, 1, @qCsat,
   NULL, 0, NULL,
   5.00, 1, NULL, 1, 1,
   N'[]', SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
  (NEWID(), @svAnnual, N'S-1404-ANNUAL', N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴', 0,
   3, @orgSal, N'/HQ/SAL', NULL, NULL, NULL,
   DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, 0, SYSUTCDATETIME()),
   2, 2, 100.00,
   -50.00, 0, 1, 1, @qNps,
   3.50, 2, @qCsat,
   NULL, 0, NULL,
   3.50, 2, NULL, 2, 1,
   N'[]', SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
  (NEWID(), @svAnnual, N'S-1404-ANNUAL', N'نظرسنجی سالانه‌ی رضایت کارکنان ۱۴۰۴', 0,
   2, NULL, NULL, @cmEmail, N'C-1404-EMAIL', NULL,
   DATEADD(DAY, -14, SYSUTCDATETIME()), DATEADD(DAY, 0, SYSUTCDATETIME()),
   5, 4, 80.00,
   0.00, 2, 2, 1, @qNps,
   4.00, 4, @qCsat,
   NULL, 0, NULL,
   3.75, 4, 66.67, 6, 2,
   N'[]', SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
  (NEWID(), @svTools, N'S-1404-TOOLS', N'نظرسنجی ابزارها و نرم‌افزارها', 1,
   1, NULL, NULL, NULL, NULL, 5,
   DATEADD(DAY, -10, SYSUTCDATETIME()), DATEADD(DAY, 0, SYSUTCDATETIME()),
   2, 1, 50.00,
   NULL, 0, 1, 0, NULL,
   4.00, 1, @qCsat,
   NULL, 0, NULL,
   4.00, 1, NULL, 0, 0,
   N'[]', SYSUTCDATETIME(), SYSUTCDATETIME(), 0);

-- بنچمارک‌ها (اهداف شاخص‌ها)
INSERT INTO benchmarks
  (Id, Name, Metric, TargetValue, IsCompanyWide, OrgUnitId, OrgUnitPath, Description,
   IsActive, CreatedAt, IsDeleted)
VALUES
  (NEWID(), N'هدف NPS سازمانی',  1, 30.00,  1, NULL, NULL, N'حداقل NPS مورد انتظار در سطح شرکت', 1, SYSUTCDATETIME(), 0),
  (NEWID(), N'هدف CSAT سازمانی', 2,  4.00,  1, NULL, NULL, N'حداقل میانگین رضایت مشتری داخلی',    1, SYSUTCDATETIME(), 0),
  (NEWID(), N'هدف نرخ تکمیل',    4, 80.00,  1, NULL, NULL, N'حداقل درصد تکمیل نظرسنجی‌ها',         1, SYSUTCDATETIME(), 0),
  (NEWID(), N'هدف NPS واحد فروش',1, 20.00,  0, @orgSal, N'/HQ/SAL', N'هدف اختصاصی واحد فروش', 1, SYSUTCDATETIME(), 0);

COMMIT TRANSACTION;
PRINT N'بخش دوم داده‌ها (پاسخ‌ها و تحلیلات) درج شد.';
