import { useMemo } from 'react';

import { QuestionType } from '@/api/questionBank';
import { useLanguage } from '@/i18n/LanguageProvider';
import { useQuestionBankSearch, useQuestionnaire } from '@/api/hooks';
import { Dialog } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { EmptyState, ErrorState } from '@/components/ui/states';
import { questionTypeLabel } from './questionType';

interface QuestionnaireDetailDialogProps {
  open: boolean;
  onClose: () => void;
  questionnaireId: string | null;
}

/**
 * دیالوگ مشاهده‌ی ساختار کامل پرسشنامه: بخش‌ها، سؤال‌ها و گزینه‌های پاسخ.
 * فقط خواندنی — برای ویرایش از جدول مدیریت اقدام ویرایش را انتخاب کنید.
 */
export function QuestionnaireDetailDialog({ open, onClose, questionnaireId }: QuestionnaireDetailDialogProps) {
  const { t } = useLanguage();

  const { data: questionnaire, isLoading, isError } = useQuestionnaire(questionnaireId);
  const { data: questionsPage } = useQuestionBankSearch({ searchText: null, type: null });

  const questionLookup = useMemo(() => {
    const map = new Map<string, { type: QuestionType; options: { code: string; text: string }[]; scaleMax: number }>();
    for (const question of questionsPage?.items ?? []) {
      map.set(question.id, {
        type: question.type,
        scaleMax: question.scaleMax,
        options: question.options.map((option) => ({ code: option.code, text: option.text }))
      });
    }
    return map;
  }, [questionsPage]);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={t.questionnaires.structureTitle}
      description={t.questionnaires.structureDescription}
      size="lg"
      footer={
        <Button variant="outline" onClick={onClose}>
          {t.common.close}
        </Button>
      }
    >
      {isError ? (
        <ErrorState message={t.errors.generic} />
      ) : isLoading || !questionnaire ? (
        <p className="text-sm text-muted-foreground">{t.common.loading}</p>
      ) : (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="outline" dir="ltr">
              {questionnaire.code}
            </Badge>
            <StatusBadge status={questionnaire.status} />
            <span className="text-xs text-muted-foreground">
              {t.questionnaires.version}: {questionnaire.version}
            </span>
            <span className="text-xs text-muted-foreground">
              {t.questionnaires.sections}: {questionnaire.sectionCount} · {t.questionnaires.questions}:{' '}
              {questionnaire.itemCount}
            </span>
          </div>

          <h3 className="text-sm font-semibold">{questionnaire.title}</h3>

          {questionnaire.sections.length === 0 ? (
            <EmptyState title={t.questionnaires.noSections} description={t.questionnaires.noSectionsDescription} />
          ) : (
            <div className="flex flex-col gap-3">
              {questionnaire.sections.map((section, sectionIndex) => (
                <div key={section.id} className="rounded-lg border bg-muted/20 p-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-sm font-medium">
                      {sectionIndex + 1}. {section.title}
                    </span>
                    {section.isOptional ? (
                      <Badge variant="outline">{t.questionnaires.optionalBadge}</Badge>
                    ) : (
                      <Badge variant="default">{t.questionnaires.requiredBadge}</Badge>
                    )}
                  </div>

                  {section.items.length === 0 ? (
                    <p className="mt-2 text-xs text-muted-foreground">{t.questionnaires.emptySection}</p>
                  ) : (
                    <ol className="mt-2 flex flex-col gap-2">
                      {section.items.map((item, itemIndex) => {
                        const question = questionLookup.get(item.questionId);

                        return (
                          <li key={item.id} className="rounded-md border bg-card p-2.5">
                            <div className="flex flex-wrap items-center gap-1.5">
                              <span className="text-xs font-medium text-muted-foreground">
                                {itemIndex + 1}.
                              </span>
                              <span className="text-sm font-medium">
                                {item.titleOverride || item.questionText}
                              </span>
                              {item.isRequired ? (
                                <Badge variant="default">{t.questionnaires.requiredBadge}</Badge>
                              ) : (
                                <Badge variant="outline">{t.questionnaires.optionalBadge}</Badge>
                              )}
                            </div>

                            <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                              <span className="rounded-sm border bg-muted px-1.5 py-0.5 text-xs" dir="ltr">
                                {item.questionCode}
                              </span>
                              <span className="rounded-sm border bg-muted px-1.5 py-0.5 text-xs">
                                {questionTypeLabel(
                                  t.questionnaires,
                                  question?.type ?? item.questionType
                                )}
                              </span>
                              {question?.type === QuestionType.Rating && (
                                <span className="rounded-sm border bg-muted px-1.5 py-0.5 text-xs">
                                  {t.questionnaires.scaleMax}: {question.scaleMax}
                                </span>
                              )}
                            </div>

                            {question && question.options.length > 0 ? (
                              <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                                <span className="text-xs text-muted-foreground">
                                  {t.questionnaires.answerOptions}:
                                </span>
                                {question.options.map((option) => (
                                  <span
                                    key={`${item.id}-${option.code}`}
                                    className="rounded-sm border bg-muted px-1.5 py-0.5 text-xs"
                                    dir="ltr"
                                  >
                                    {option.code} — {option.text}
                                  </span>
                                ))}
                              </div>
                            ) : (
                              <p className="mt-1.5 text-xs text-muted-foreground">
                                {t.questionnaires.noAnswerOptions}
                              </p>
                            )}
                          </li>
                        );
                      })}
                    </ol>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </Dialog>
  );
}

function StatusBadge({ status }: { status: number }) {
  const { t } = useLanguage();

  switch (status) {
    case 2:
      return <Badge variant="success">{t.questionnaires.activeStatus}</Badge>;
    case 3:
      return <Badge variant="outline">{t.questionnaires.archived}</Badge>;
    default:
      return <Badge variant="warning">{t.questionnaires.draft}</Badge>;
  }
}
