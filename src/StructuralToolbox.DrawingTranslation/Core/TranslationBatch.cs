using System;
using System.Collections.Generic;

namespace StructuralToolbox.DrawingTranslation.Core
{
    public sealed class TranslationBatchResult
    {
        public TranslationBatchResult(IList<DrawingTextItem> items)
        {
            Items = items ?? new List<DrawingTextItem>();
        }

        public IList<DrawingTextItem> Items { get; private set; }
        public int ReadyCount { get { return Count(TranslationItemStatus.Ready); } }
        public int SkippedCount { get { return Count(TranslationItemStatus.Skipped); } }
        public int FailedCount { get { return Count(TranslationItemStatus.Failed); } }
        public int StaleCount { get { return Count(TranslationItemStatus.Stale); } }

        private int Count(TranslationItemStatus status)
        {
            int count = 0;
            foreach (DrawingTextItem item in Items)
                if (item != null && item.Status == status)
                    count++;
            return count;
        }
    }

    public static class TranslationBatch
    {
        public static TranslationBatchResult Apply(IList<DrawingTextItem> items,
            IList<string> translatedTemplates)
        {
            if (items == null)
                throw new ArgumentNullException("items");
            if (translatedTemplates == null)
                throw new ArgumentNullException("translatedTemplates");

            int required = 0;
            foreach (DrawingTextItem item in items)
                if (item != null && item.CanWrite &&
                    item.Status == TranslationItemStatus.Ready)
                    required++;
            if (translatedTemplates.Count > required)
                throw new InvalidOperationException(
                    "翻译服务返回了多余条目，拒绝继续处理。");

            int inputIndex = 0;
            foreach (DrawingTextItem item in items)
            {
                if (item == null || !item.CanWrite ||
                    item.Status != TranslationItemStatus.Ready)
                    continue;
                if (inputIndex >= translatedTemplates.Count)
                {
                    item.Status = TranslationItemStatus.Failed;
                    item.FailureReason = "翻译服务返回条目数量不足。";
                    continue;
                }

                string translated = translatedTemplates[inputIndex++];
                if (String.IsNullOrWhiteSpace(translated))
                {
                    item.Status = TranslationItemStatus.Failed;
                    item.FailureReason = "翻译结果为空，保留原文。";
                    continue;
                }
                string restored;
                string error;
                if (!item.Protection.TryRestore(translated, out restored,
                    out error))
                {
                    item.Status = TranslationItemStatus.Failed;
                    item.FailureReason = error;
                    continue;
                }
                item.Translation = restored;
            }
            return new TranslationBatchResult(items);
        }
    }
}
