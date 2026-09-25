import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { CheckCircle2, Save, Send } from 'lucide-react';

import { ApiError } from '@/api/client';
import {
  BranchingCondition,
  QuestionType,
  ResponseStatus,
  type RespondentItem,
  type RespondentSurveyContext,
  type SaveAnswerRequest
} from '@/api/responses';
import { useLanguage } from '@/i18n/LanguageProvider';
import {
  queryKeys,
  useRespondentContext,
  useSaveAnswers,
  useStartSession,
  useSubmitResponse
} from '@/api/hooks';
import { useQueryClient } from '@tanstack/react-query';
import { AppLayout } from '@/layouts/AppLayout';
import { PageHeader } from '@/components/ui/page-header';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { ErrorState } from '@/components/ui/states';
import { FormField } from '@/components/ui/form-field';
import { Spinner } from '@/components/ui/spinner';

/**
 * صفحه‌ی پاسخ‌گویی به یک نظرسنجی: ساختار پرسشنامه را نشان می‌دهد، پاسخ‌ها را
 * به‌صورت موقت نگه می‌دارد، امکان ذخیره‌ی جزئی و ارسال نهایی را فراهم می‌کند.
 *
 * نشست به‌صورت خودکار هنگام ورود شروع یا از سرگیری می‌شود. ارسال نهایی،
 * اعتبارسنجی سؤال‌های اجباری را سمت سرور انجام می‌دهد.
 */
export function RespondentSurveyPage() {
  const { t, culture } = useLanguage();
  const navigate = useNavigate();
  const { surveyId } = useParams<{ surveyId: string }>();
  const queryClient = useQueryClient();

  const { data, isLoading, isError, error, refetch } = useRespondentContext(surveyId ?? null);

  const [draft, setDraft] = useState<Record<string, SaveAnswerRequest>>({});
  const [submitOpen, setSubmitOpen] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const startSession = useStartSession();
  const saveAnswers = useSaveAnswers();
  const submitResponse = useSubmitResponse();

  const sessionId = data?.session?.id ?? null;
  const isSubmitted = data?.session?.status === ResponseStatus.Submitted;
  const readOnly = isSubmitted && data?.allowEditResponse !== true;

  // شروع خودکار نشست هنگام بارگذاری اولیه‌ی بسته‌ی پاسخ‌گویی.
  useEffect(() => {
    if (!data || data.session) {
      return;
    }

    startSession.mutate(
      { surveyId: data.surveyId, responseLanguage: 1 },
      {
        onSuccess: () => {
          void queryClient.invalidateQueries({ queryKey: queryKeys.respondentContext(data.surveyId) });
        }
      }
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data?.surveyId, data?.session]);

  // مقداردهی اولیه‌ی پیش‌نویس از پاسخ‌های ذخیره‌شده‌ی نشست (ذخیره‌ی جزئی یا
  // ارسال‌شده‌ی قابل‌ویرایش) تا کاربر از جایی که قبلاً بوده ادامه دهد.
  useEffect(() => {
    if (!data?.session) {
      return;
    }

    setDraft((previous) => {
      const next: Record<string, SaveAnswerRequest> = {};

      for (const section of data.sections) {
        for (const item of section.items) {
          // تغییرات محلی کاربر روی پیش‌نویس موجود ارجحیت دارند.
          next[item.id] = previous[item.id] ?? initialAnswer(item, data.session);
        }
      }

      return next;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data?.session?.id]);

  const answers = useMemo(() => Object.values(draft), [draft]);

  const answeredCount = answers.filter((answer) => hasValue(answer)).length;
  const totalCount = data?.sections.reduce((sum, section) => sum + section.items.length, 0) ?? 0;

  function handleDraftChange(itemId: string, answer: SaveAnswerRequest) {
    setDraft((previous) => ({ ...previous, [itemId]: answer }));
  }

  async function handleSaveDraft() {
    if (!sessionId) {
      return;
    }

    saveAnswers.mutate({ sessionId, request: { answers } });
  }

  async function handleSubmit() {
    if (!sessionId) {
      return;
    }

    setSubmitError(null);

    submitResponse.mutate(
      { sessionId, request: { answers } },
      {
        onSuccess: () => {
          setSubmitOpen(false);
          void queryClient.invalidateQueries({ queryKey: queryKeys.respondentContext(surveyId as string) });
        },
        onError: (submissionError) => {
          setSubmitError(errorMessage(submissionError));
        }
      }
    );
  }

  if (isLoading) {
    return (
      <AppLayout>
        <PageHeader title={t.responses.answeringTitle} />
        <div className="flex flex-col gap-4">
          {Array.from({ length: 2 }).map((_, index) => (
            <Card key={index} className="h-44 animate-pulse bg-muted" />
          ))}
        </div>
      </AppLayout>
    );
  }

  if (isError || !data) {
    return (
      <AppLayout>
        <PageHeader title={t.responses.answeringTitle} />
        <ErrorState
          message={(error as ApiError)?.problem.detail ?? t.responses.errorQuestionnaireNotAvailable}
          onRetry={() => void refetch()}
        />
      </AppLayout>
    );
  }

  if (readOnly) {
    return <ThankYou context={data} onBack={() => navigate(`/${culture}/my-surveys`)} />;
  }

  const visibleItems = computeVisibleItems(data, draft);

  return (
    <AppLayout>
      <PageHeader
        title={data.title}
        description={data.welcomeMessage ?? data.description ?? undefined}
        actions={
          <Badge variant="outline">
            {t.responses.answerCount}: {answeredCount} / {totalCount}
          </Badge>
        }
      />

      {data.isAnonymous && (
        <p className="mb-4 text-sm text-muted-foreground">{t.responses.anonymousNote}</p>
      )}

      {startSession.isError && (
        <ErrorState message={errorMessage(startSession.error)} className="mb-4" />
      )}

      <div className="flex flex-col gap-6">
        {data.sections.map((section) => {
          const sectionItems = section.items.filter((item) => visibleItems.has(item.id));

          if (sectionItems.length === 0) {
            return null;
          }

          return (
            <Card key={section.id} className="flex flex-col gap-5 p-6">
              <div className="flex flex-col gap-1">
                <h2 className="text-lg font-semibold">{section.title}</h2>
                <span className="text-xs text-muted-foreground">
                  {t.responses.section} {section.displayOrder + 1}
                  {section.isOptional ? ` · ${t.responses.optionalHint}` : ` · ${t.responses.requiredHint}`}
                </span>
              </div>

              <div className="flex flex-col gap-6">
                {sectionItems.map((item) => (
                  <QuestionInput
                    key={item.id}
                    item={item}
                    initial={initialAnswer(item, data.session)}
                    onChange={(answer) => handleDraftChange(item.id, answer)}
                  />
                ))}
              </div>
            </Card>
          );
        })}
      </div>

      <div className="mt-6 flex flex-wrap items-center justify-end gap-2">
        <Button
          variant="outline"
          onClick={handleSaveDraft}
          disabled={saveAnswers.isPending || !sessionId}
        >
          {saveAnswers.isPending ? (
            <Spinner className="size-4" />
          ) : (
            <Save className="size-4" />
          )}
          {saveAnswers.isPending ? t.responses.savingDraft : t.responses.saveDraft}
        </Button>

        <Button onClick={() => setSubmitOpen(true)} disabled={submitResponse.isPending || !sessionId}>
          <Send className="size-4" />
          {t.responses.submitResponse}
        </Button>
      </div>

      <ConfirmDialog
        open={submitOpen}
        onClose={() => setSubmitOpen(false)}
        title={t.responses.submitConfirmTitle}
        description={t.responses.submitConfirmDescription}
        confirmLabel={t.responses.submitResponse}
        onConfirm={handleSubmit}
        isPending={submitResponse.isPending}
        error={submitError ?? undefined}
        destructive={false}
      />
    </AppLayout>
  );
}

function ThankYou({
  context,
  onBack
}: {
  context: RespondentSurveyContext;
  onBack: () => void;
}) {
  const { t } = useLanguage();

  return (
    <AppLayout>
      <PageHeader title={t.responses.thankYou} />
      <Card className="flex flex-col items-center gap-4 p-10 text-center">
        <CheckCircle2 className="size-12 text-primary" />
        <div className="flex flex-col gap-1">
          <h2 className="text-lg font-semibold">{t.responses.responseSubmitted}</h2>
          <p className="text-sm text-muted-foreground">
            {context.thankYouMessage ?? t.responses.thankYouDescription}
          </p>
        </div>
        <Button variant="outline" onClick={onBack}>
          {t.responses.backToMySurveys}
        </Button>
      </Card>
    </AppLayout>
  );
}

function QuestionInput({
  item,
  initial,
  onChange
}: {
  item: RespondentItem;
  initial: SaveAnswerRequest;
  onChange: (answer: SaveAnswerRequest) => void;
}) {
  const { t } = useLanguage();
  const fieldId = `item-${item.id}`;

  const base = {
    questionnaireItemId: item.id,
    textValue: initial.textValue ?? null,
    numericValue: initial.numericValue ?? null,
    selectedOptionIds: initial.selectedOptionIds ?? []
  };

  const isChoice =
    item.questionType === QuestionType.SingleChoice ||
    item.questionType === QuestionType.MultipleChoice;

  return (
    <FormField
      label={item.text}
      htmlFor={fieldId}
      required={item.isRequired}
      hint={item.description ?? undefined}
    >
      {isChoice ? (
        <ChoiceControl item={item} fieldId={fieldId} initial={base} onChange={onChange} />
      ) : (
        <TextOrNumberControl item={item} fieldId={fieldId} initial={base} onChange={onChange} t={t} />
      )}
    </FormField>
  );
}

function ChoiceControl({
  item,
  fieldId,
  initial,
  onChange
}: {
  item: RespondentItem;
  fieldId: string;
  initial: SaveAnswerRequest;
  onChange: (answer: SaveAnswerRequest) => void;
}) {
  const { t } = useLanguage();
  const selected = initial.selectedOptionIds ?? [];

  return (
    <div className="flex flex-col gap-2">
      {item.options.map((option) => {
        const isSelected = selected.includes(option.id);

        function toggle() {
          if (item.questionType === QuestionType.SingleChoice) {
            onChange({ ...initial, selectedOptionIds: [option.id], textValue: null, numericValue: null });
            return;
          }

          const next = isSelected
            ? selected.filter((id) => id !== option.id)
            : [...selected, option.id];

          onChange({ ...initial, selectedOptionIds: next, textValue: null, numericValue: null });
        }

        return (
          <label
            key={option.id}
            className="flex cursor-pointer items-center gap-3 rounded-md border p-3 text-sm transition-colors hover:bg-accent"
          >
            <input
              type={item.questionType === QuestionType.SingleChoice ? 'radio' : 'checkbox'}
              name={fieldId}
              checked={isSelected}
              onChange={toggle}
              className="size-4 accent-primary"
            />
            <span>{option.text}</span>
          </label>
        );
      })}
      {selected.length === 0 && (
        <span className="sr-only">{t.responses.selectOption}</span>
      )}
    </div>
  );
}

function TextOrNumberControl({
  item,
  fieldId,
  initial,
  onChange,
  t
}: {
  item: RespondentItem;
  fieldId: string;
  initial: SaveAnswerRequest;
  onChange: (answer: SaveAnswerRequest) => void;
  t: ReturnType<typeof useLanguage>['t'];
}) {
  if (item.questionType === QuestionType.YesNo) {
    return (
      <div className="flex gap-2">
        {['yes', 'no'].map((value) => (
          <Button
            key={value}
            type="button"
            variant={initial.textValue === value ? 'default' : 'outline'}
            onClick={() => onChange({ ...initial, textValue: value, numericValue: null })}
          >
            {value === 'yes' ? t.common.yes : t.common.no}
          </Button>
        ))}
      </div>
    );
  }

  if (item.questionType === QuestionType.Rating || item.questionType === QuestionType.Number) {
    return (
      <Input
        id={fieldId}
        type="number"
        min={1}
        max={item.scaleMax > 0 ? item.scaleMax : undefined}
        value={initial.numericValue ?? ''}
        onChange={(event) =>
          onChange({
            ...initial,
            numericValue: event.target.value === '' ? null : Number(event.target.value),
            textValue: null
          })
        }
        placeholder={
          item.questionType === QuestionType.Rating ? t.responses.enterRating : t.responses.enterNumber
        }
      />
    );
  }

  if (item.questionType === QuestionType.LongText) {
    return (
      <textarea
        id={fieldId}
        value={initial.textValue ?? ''}
        onChange={(event) => onChange({ ...initial, textValue: event.target.value, numericValue: null })}
        placeholder={t.responses.enterText}
        rows={4}
        className="w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
      />
    );
  }

  return (
    <Input
      id={fieldId}
      value={initial.textValue ?? ''}
      onChange={(event) => onChange({ ...initial, textValue: event.target.value, numericValue: null })}
      placeholder={t.responses.enterText}
    />
  );
}

/**
 * آیتم‌هایی که باید نمایش داده شوند: انشعابِ برقرارشده‌ی سمت کلاینت، آیتم
 * مقصد را مخفی می‌کند. این ارزیابی فقط برای تجربه‌ی کاربری است — مرز واقعی
 * سمت سرور اعمال می‌شود.
 */
function computeVisibleItems(
  context: RespondentSurveyContext,
  draft: Record<string, SaveAnswerRequest>
): Set<string> {
  const hidden = new Set<string>();

  for (const section of context.sections) {
    for (const item of section.items) {
      const answer = draft[item.id];
      if (!answer || !hasValue(answer)) {
        continue;
      }

      for (const rule of item.branchingRules) {
        if (isBranchActive(rule.condition, rule.expectedValue, item.questionType, answer)) {
          hidden.add(rule.targetItemId);
        }
      }
    }
  }

  const visible = new Set<string>();
  for (const section of context.sections) {
    for (const item of section.items) {
      if (!hidden.has(item.id)) {
        visible.add(item.id);
      }
    }
  }

  return visible;
}

function isBranchActive(
  condition: BranchingCondition,
  expected: string,
  questionType: QuestionType,
  answer: SaveAnswerRequest
): boolean {
  const options = answer.selectedOptionIds ?? [];

  if (questionType === QuestionType.SingleChoice || questionType === QuestionType.MultipleChoice) {
    switch (condition) {
      case BranchingCondition.Equals:
        return options.length === 1 && options[0] === expected;
      case BranchingCondition.NotEquals:
        return options.length !== 1 || options[0] !== expected;
      case BranchingCondition.Contains:
        return options.includes(expected);
      default:
        return false;
    }
  }

  const numeric = answer.numericValue ?? null;

  if (questionType === QuestionType.Rating || questionType === QuestionType.Number) {
    const expectedNumber = Number(expected);

    switch (condition) {
      case BranchingCondition.Equals:
        return numeric !== null && numeric === expectedNumber;
      case BranchingCondition.NotEquals:
        return numeric === null || numeric !== expectedNumber;
      case BranchingCondition.GreaterThan:
        return numeric !== null && numeric > expectedNumber;
      case BranchingCondition.LessThan:
        return numeric !== null && numeric < expectedNumber;
      default:
        return false;
    }
  }

  const text = answer.textValue ?? '';

  switch (condition) {
    case BranchingCondition.Equals:
      return text.trim() === expected;
    case BranchingCondition.NotEquals:
      return text.trim() !== expected;
    default:
      return false;
  }
}

function hasValue(answer: SaveAnswerRequest): boolean {
  return (
    (answer.selectedOptionIds?.length ?? 0) > 0 ||
    answer.numericValue !== null ||
    (answer.textValue?.trim().length ?? 0) > 0
  );
}

function initialAnswer(
  item: RespondentItem,
  session: RespondentSurveyContext['session']
): SaveAnswerRequest {
  const existing = session?.answers.find((answer) => answer.questionnaireItemId === item.id);

  if (!existing) {
    return {
      questionnaireItemId: item.id,
      textValue: null,
      numericValue: null,
      selectedOptionIds: []
    };
  }

  return {
    questionnaireItemId: item.id,
    textValue: existing.textValue ?? null,
    numericValue: existing.numericValue ?? null,
    selectedOptionIds: existing.selections.map((selection) => selection.optionId)
  };
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.problem.detail ?? error.problem.extensions?.code ?? error.message;
  }

  return String(error);
}
