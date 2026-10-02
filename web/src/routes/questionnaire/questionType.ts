import { QuestionType } from '@/api/questionBank';
import type { Dictionary } from '@/i18n/types';

type QuestionnairesDict = Dictionary['questionnaires'];

/** برچسب نمایشی کوتاه برای نوع سؤال، بر اساس دیکشنری فعال. */
export function questionTypeLabel(t: QuestionnairesDict, type: QuestionType | number): string {
  const dict = t as QuestionnairesDict;
  switch (type) {
    case QuestionType.SingleChoice: return dict.typeSingleChoice;
    case QuestionType.MultipleChoice: return dict.typeMultipleChoice;
    case QuestionType.Rating: return dict.typeRating;
    case QuestionType.YesNo: return dict.typeYesNo;
    case QuestionType.ShortText: return dict.typeShortText;
    case QuestionType.LongText: return dict.typeLongText;
    case QuestionType.Number: return dict.typeNumber;
    default: return String(type);
  }
}

/** آیا این نوع سؤال نیاز به گزینه‌های پاسخ دارد؟ قرینه‌ی سمت سرور. */
export function typeHasOptions(type: QuestionType | number): boolean {
  return type === QuestionType.SingleChoice || type === QuestionType.MultipleChoice;
}
