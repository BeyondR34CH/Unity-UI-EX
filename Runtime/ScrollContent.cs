using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityEngine.UI.EX
{
    [AddComponentMenu("Layout/Scroll Content", 37)]
    [SelectionBase]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(RectTransform))]
    public class ScrollContent : ScrollList, ILayoutSelfController
    {
        [SerializeField] private ListAnchor m_Alignment = ListAnchor.MiddleCenter;
        public ListAnchor alignment { get { return m_Alignment; } set { m_Alignment = value; UpdateItems(true, true, false); } }

        public override void SetLayoutHorizontal()
        {
            m_Tracker.Add(this, rectTransform,
                DrivenTransformProperties.SizeDelta | DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot
            );
            SetSelfSize();
            SetSelfRect(false);

            base.SetLayoutHorizontal();
        }

        public override void UpdateItemDatas(bool resetPos = false, bool updateSize = false, bool updatePrefab = false)
        {
            SetItems(updateSize, updatePrefab);
            SetSelfSize();
            UpdateItems(true, true, resetPos);
        }

        protected override void UpdateItems(bool updateCheckValue, bool forceDirty, bool resetPos)
        {
            if (updateCheckValue) UpdateCheckValue();
            SetSelfRect(resetPos);
            SetItemsShow(forceDirty);
        }

        private void SetSelfSize()
        {
            if (isVertical)
                rectTransform.sizeDelta = new Vector2(0, m_TotalPreferredSize.y);
            else
                rectTransform.sizeDelta = new Vector2(m_TotalPreferredSize.x, 0);
        }

        private void SetSelfRect(bool resetPos)
        {
            if (isVertical)
            {
                var pivot = 1 - (rectTransform.sizeDelta.y <= m_ParentSize.y ? (int)alignment * 0.5f : GetAlignmentOnAxis(1));
                if (pivot != rectTransform.pivot.y) rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, 0);
                rectTransform.pivot = new Vector2(0.5f, pivot);
                rectTransform.anchorMin = new Vector2(0f, pivot);
                rectTransform.anchorMax = new Vector2(1f, pivot);
            }
            else
            {
                var pivot = rectTransform.sizeDelta.x <= m_ParentSize.x ? (int)alignment * 0.5f : GetAlignmentOnAxis(0);
                if (pivot != rectTransform.pivot.x) rectTransform.anchoredPosition = new Vector2(0, rectTransform.anchoredPosition.y);
                rectTransform.pivot = new Vector2(pivot, 0.5f);
                rectTransform.anchorMin = new Vector2(pivot, 0f);
                rectTransform.anchorMax = new Vector2(pivot, 1f);
            }

            if (resetPos) rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
