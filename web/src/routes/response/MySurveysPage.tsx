import { useNavigate } from 'react-router-dom';
import { ClipboardList, FileText, Clock, ShieldOff } from 'lucide-react';

import { ApiError } from '@/api/client';
import { ResponseStatus, type RespondableSurvey } from '@/api/responses';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useMySurveys } from '@/api/hooks';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState, ErrorState } from '@/components/ui/states';

/**
 * صفحه‌ی «نظرسنجی‌های من»: نظرسنجی‌های پاسخ‌پذیری که کاربر جاری به آن‌ها
 * دعوت شده (یا عموماً باز هستند). وضعیت نشست کاربر برای هر نظرسنجی نشان
 * داده می‌شود تا بداند کدام را شروع کرده، کدام را ارسال کرده و کدام را
 * می‌تواند ویرایش کند.
 */
export function MySurveysPage() {
  const { t, culture } = useLanguage();
  const navigate = useNavigate();

  const { data, isLoading, isError, error, refetch } = useMySurveys(1);

  const surveys = data?.items ?? [];

  function handleOpen(survey: RespondableSurvey) {
    navigate(`/${culture}/respond/${survey.surveyId}`);
  }

  function actionLabel(survey: RespondableSurvey): string {
    switch (survey.sessionStatus) {
      case ResponseStatus.Submitted:
        return t.responses.reviewResponse;
      case ResponseStatus.InProgress:
        return t.responses.continueResponse;
      default:
        return t.responses.startResponse;
    }
  }

  return (
    <AppLayout>
      <PageHeader title={t.responses.mySurveys} description={t.responses.mySurveysDescription} />

      {isError ? (
        <ErrorState message={(error as ApiError)?.problem.detail} onRetry={() => void refetch()} />
      ) : isLoading ? (
        <div className="flex flex-col gap-4">
          {Array.from({ length: 3 }).map((_, index) => (
            <Card key={index} className="h-28 animate-pulse bg-muted" />
          ))}
        </div>
      ) : surveys.length === 0 ? (
        <EmptyState
          title={t.responses.noSurveys}
          description={t.responses.noSurveysDescription}
          icon={<ClipboardList className="size-10" />}
        />
      ) : (
        <div className="flex flex-col gap-4">
          {surveys.map((survey) => (
            <Card key={survey.surveyId} className="flex flex-col gap-3 p-5">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex flex-col gap-1">
                  <div className="flex items-center gap-2">
                    <h3 className="text-base font-semibold">{survey.title}</h3>
                    {survey.isAnonymous && (
                      <Badge variant="outline" className="gap-1">
                        <ShieldOff className="size-3" />
                        {t.responses.anonymous}
                      </Badge>
                    )}
                  </div>
                  <span className="font-mono text-xs text-muted-foreground" dir="ltr">
                    {survey.surveyCode}
                  </span>
                  {survey.description && (
                    <p className="text-sm text-muted-foreground">{survey.description}</p>
                  )}
                </div>

                <SessionBadge status={survey.sessionStatus ?? null} />
              </div>

              <div className="flex flex-wrap items-center gap-4 text-sm text-muted-foreground">
                <span className="flex items-center gap-1">
                  <Clock className="size-4" />
                  {t.responses.estimatedMinutes}: {survey.estimatedMinutes}
                </span>

                {survey.campaignCode && (
                  <span className="flex items-center gap-1">
                    <FileText className="size-4" />
                    {t.responses.invitedViaCampaign}: {survey.campaignCode}
                  </span>
                )}
              </div>

              {survey.isAnonymous && (
                <p className="text-xs text-muted-foreground">{t.responses.anonymousNote}</p>
              )}

              <div className="flex justify-end">
                <Button onClick={() => handleOpen(survey)}>{actionLabel(survey)}</Button>
              </div>
            </Card>
          ))}
        </div>
      )}
    </AppLayout>
  );
}

function SessionBadge({ status }: { status: ResponseStatus | null }) {
  const { t } = useLanguage();

  if (status === null) {
    return null;
  }

  if (status === ResponseStatus.Submitted) {
    return <Badge>{t.responses.statusSubmitted}</Badge>;
  }

  return <Badge variant="outline">{t.responses.statusInProgress}</Badge>;
}
