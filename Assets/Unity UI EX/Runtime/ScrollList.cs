using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityEngine.UI.EX
{
    [AddComponentMenu("Layout/Scroll List", 37)]
    [SelectionBase]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(RectTransform))]
    public class ScrollList : UIBehaviour, ILayoutElement, ILayoutGroup, ILayoutSelfController
    {
        public enum ListAnchor { UpperLeft, MiddleCenter, LowerRight }

        private class ScrollItem
        {
            private int m_PrefabID;
            private GameObject m_Prefab;

            public float position { get; set; }
            public float size { get; set; }
            public int prefabID => m_PrefabID;
            public GameObject prefab { get { return m_Prefab; } set { m_Prefab = value; m_PrefabID = m_Prefab ? m_Prefab.GetInstanceID() : 0; } }
            public GameObject gameObject { get; set; }

            public bool IsVisible(float viewHead, float viewTail)
            {
                var posTail = position + size;
                return position >= viewHead && position < viewTail || posTail > viewHead && posTail <= viewTail;
            }
        }

        [SerializeField] private RectTransform.Axis m_LayoutAxis = RectTransform.Axis.Vertical;
        public RectTransform.Axis layoutAxis { get { return m_LayoutAxis; } set { m_LayoutAxis = value; } }

        private bool isVertical => layoutAxis == RectTransform.Axis.Vertical;

        [SerializeField] private ListAnchor m_Alignment = ListAnchor.MiddleCenter;
        public ListAnchor alignment { get { return m_Alignment; } set { m_Alignment = value; UpdateItemData(); SetDirty(); } }

        private int m_ItemCount;
        private Func<int, float> m_OnGetItemSize;
        private Func<int, GameObject> m_OnGetPrefab;
        private Action<int, GameObject> m_OnUpdateItem;

        [NonSerialized] private int m_HeadIndex, m_TailIndex;
        [NonSerialized] private readonly List<ScrollItem> m_ScrollItems = new();
        [NonSerialized] private readonly Dictionary<int, Queue<GameObject>> m_PrefabPools = new();

        #region RectTransform Control

        [NonSerialized] private RectTransform m_Rect;
        public RectTransform rectTransform => m_Rect ? m_Rect : m_Rect = GetComponent<RectTransform>();

        // private bool isRootLayoutGroup => !transform.parent || transform.parent.GetComponent(typeof(ILayoutGroup)) is not Behaviour behaviour || !behaviour.enabled;

        [NonSerialized] private DrivenRectTransformTracker m_Tracker;

        #endregion

        #region Behaviour

        protected ScrollList() { }

        protected override void OnEnable()
        {
            m_IsDirty = false;
            m_Position = rectTransform.anchoredPosition;
            m_ParentSize = transform.parent is RectTransform parentRT ? parentRT.rect.size : Vector2.zero;
            UpdateItem(true);
        }

        protected override void OnDisable()
        {
            m_Tracker.Clear();
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        protected virtual void LateUpdate()
        {
            var parentRectSizeChange = CheckParentRectSize();
            var anchoredPosChange = CheckAnchoredPos();

            if (parentRectSizeChange || anchoredPosChange)
            {
                UpdateItem(parentRectSizeChange);
            }
        }

        // protected override void OnRectTransformDimensionsChange()
        // {
        //     if (isRootLayoutGroup) SetDirty();
        // }

        // protected virtual void OnTransformChildrenChanged()
        // {
        //     SetDirty();
        // }

        // protected override void OnTransformParentChanged()
        // {
        //     SetDirty();
        // }

        // protected override void OnDidApplyAnimationProperties()
        // {
        //     SetDirty();
        // }

        [NonSerialized] private Vector2 m_ParentSize;
        private bool CheckParentRectSize()
        {
            var size = transform.parent is RectTransform parentRT ? parentRT.rect.size : Vector2.zero;
            if (m_ParentSize != size)
            {
                m_ParentSize = size;
                return true;
            }
            else return false;
        }

        [NonSerialized] private Vector2 m_Position;
        private bool CheckAnchoredPos()
        {
            var pos = rectTransform.anchoredPosition;
            if (m_Position != pos)
            {
                m_Position = pos;
                return true;
            }
            else return false;
        }

        #endregion

        #region LayoutElement

        public virtual void CalculateLayoutInputHorizontal()
        {
            m_IsDirty = false;
            m_Tracker.Clear();

            CalcAlongAxis(0);
        }

        public virtual void CalculateLayoutInputVertical()
        {
            CalcAlongAxis(1);
        }

        #region LayoutProperties

        private Vector2 m_TotalMinSize = Vector2.zero;
        private Vector2 m_TotalPreferredSize = Vector2.zero;
        private Vector2 m_TotalFlexibleSize = Vector2.zero;

        public virtual float minWidth => m_TotalMinSize[0];

        public virtual float preferredWidth => m_TotalPreferredSize[0];

        public virtual float flexibleWidth => m_TotalFlexibleSize[0];

        public virtual float minHeight => m_TotalMinSize[1];

        public virtual float preferredHeight => m_TotalPreferredSize[1];

        public virtual float flexibleHeight => m_TotalFlexibleSize[1];

        public virtual int layoutPriority => 0;

        #endregion

        public virtual void SetLayoutHorizontal()
        {
            SetSelfRect();
            SetChildrenAlongAxis(0);
        }

        public virtual void SetLayoutVertical()
        {
            SetChildrenAlongAxis(1);
        }

        #endregion

        #region ScrollItems

        public void SetItemData(int itemCount, Func<int, float> onGetItemSize, Func<int, GameObject> onGetPrefab, Action<int, GameObject> onUpdateItem, bool clearPools = true)
        {
            m_ItemCount = Math.Max(itemCount, 0);
            m_OnGetItemSize = onGetItemSize;
            m_OnGetPrefab = onGetPrefab;
            m_OnUpdateItem = onUpdateItem;

            UpdateItemData(true, true, clearPools);
        }

        public void UpdateItemData(bool updateSize = false, bool updatePrefab = false, bool clearPools = false)
        {
            if (m_ScrollItems.Count < m_ItemCount)
            {
                int i = 0, count = m_ItemCount - m_ScrollItems.Count;
                while (i++ < count) m_ScrollItems.Add(new ScrollItem());
            }
            else if (m_ScrollItems.Count > m_ItemCount)
            {
                for (int i = m_ItemCount; i < m_ScrollItems.Count; i++)
                {
                    var item = m_ScrollItems[i];
                    HideItem(item);
                }
                m_ScrollItems.RemoveRange(m_ItemCount, m_ScrollItems.Count - m_ItemCount);
            }

            int axis = (int)layoutAxis;
            float pos = axis == 0 ? padding.left : padding.top;

            int startIndex = m_ReverseArrangement ? m_ItemCount - 1 : 0;
            int endIndex = m_ReverseArrangement ? 0 : m_ItemCount;
            int increment = m_ReverseArrangement ? -1 : 1;

            updateSize &= m_OnGetItemSize != null;
            updatePrefab &= m_OnGetPrefab != null;

            for (int i = startIndex; m_ReverseArrangement ? i >= endIndex : i < endIndex; i += increment)
            {
                var item = m_ScrollItems[i];
                item.position = pos;
                if (updateSize) item.size = m_OnGetItemSize(i);

                if (updatePrefab)
                {
                    var nextPrefab = m_OnGetPrefab(i);
                    if (item.prefab != nextPrefab) HideItem(item);
                    item.prefab = nextPrefab;
                }

                pos += item.size + spacing;
            }
            m_TotalPreferredSize[axis] = pos - (m_ItemCount > 0 ? spacing : 0) + (axis == 0 ? padding.right : padding.bottom);

            if (clearPools) ClearPools();

            UpdateItem(true);
        }

        public void ClearPools()
        {
            foreach (var pool in m_PrefabPools.Values)
            {
                while (pool.Count > 0) Destroy(pool.Dequeue());
            }
            m_PrefabPools.Clear();
        }

        private void UpdateItem(bool forceDirty = false)
        {
            var axis = (int)layoutAxis;
            var headPos = axis == 0
                ? rectTransform.pivot[axis] * rectTransform.sizeDelta[axis] - rectTransform.anchoredPosition[axis]
                : (1 - rectTransform.pivot[axis]) * rectTransform.sizeDelta[axis] + rectTransform.anchoredPosition[axis];
            var tailPos = headPos + m_ParentSize[axis];

            int curIndex;
            var isDirty = false;

            for (curIndex = 0; curIndex < m_ItemCount; curIndex++)
            {
                var item = m_ScrollItems[curIndex];
                if (!item.IsVisible(headPos, tailPos))
                {
                    if (HideItem(item)) isDirty = true;
                }
                else break;
            }
            for (m_HeadIndex = curIndex; curIndex < m_ItemCount; curIndex++)
            {
                var item = m_ScrollItems[curIndex];
                if (!item.IsVisible(headPos, tailPos)) break;
            }
            for (m_TailIndex = curIndex; curIndex < m_ItemCount; curIndex++)
            {
                var item = m_ScrollItems[curIndex];
                if (HideItem(item)) isDirty = true;
            }
            for (int i = m_HeadIndex; i < m_TailIndex; i++)
            {
                if (ShowItem(m_ScrollItems[i], i)) isDirty = true;
            }

            if (isDirty || forceDirty) SetDirty();
        }

        private bool ShowItem(ScrollItem item, int updateIndex)
        {
            if (item.gameObject) return false;

            if (!m_PrefabPools.TryGetValue(item.prefabID, out var itemPool)) m_PrefabPools[item.prefabID] = itemPool = new();
            item.gameObject = itemPool.Count > 0 ? itemPool.Dequeue() : Instantiate(item.prefab, transform);
            m_OnUpdateItem?.Invoke(updateIndex, item.gameObject);
            item.gameObject.SetActive(true);

            return true;
        }

        private bool HideItem(ScrollItem item)
        {
            if (!item.gameObject) return false;

            item.gameObject.SetActive(false);
            if (m_PrefabPools.TryGetValue(item.prefabID, out var itemPool)) itemPool.Enqueue(item.gameObject);
            else Destroy(item.gameObject);
            item.gameObject = null;

            return true;
        }

        private void SetSelfRect()
        {
            m_Tracker.Add(this, rectTransform,
                DrivenTransformProperties.SizeDelta | DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot
            );

            if (isVertical)
            {
                rectTransform.sizeDelta = new Vector2(0, LayoutUtility.GetPreferredSize(rectTransform, 1));
                rectTransform.anchorMin = Vector2.up;
                rectTransform.anchorMax = Vector2.one;

                var pivot = 1 - (rectTransform.sizeDelta.y <= m_ParentSize.y ? (int)alignment * 0.5f : GetAlignmentOnAxis(1));
                var pivotChange = pivot != rectTransform.pivot.y;
                rectTransform.pivot = new Vector2(0.5f, pivot);
                rectTransform.anchoredPosition = new Vector2(
                    rectTransform.anchoredPosition.x, pivotChange ? -((1 - pivot) * m_ParentSize.y) : rectTransform.anchoredPosition.y
                );
            }
            else
            {
                rectTransform.sizeDelta = new Vector2(LayoutUtility.GetPreferredSize(rectTransform, 0), 0);
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.up;

                var pivot = rectTransform.sizeDelta.x <= m_ParentSize.x ? (int)alignment * 0.5f : GetAlignmentOnAxis(0);
                var pivotChange = pivot != rectTransform.pivot.x;
                rectTransform.pivot = new Vector2(pivot, 0.5f);
                rectTransform.anchoredPosition = new Vector2(
                    pivotChange ? pivot * m_ParentSize.x : rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y
                );
            }
        }

        #endregion

        #region LayoutGroup

        #region LayoutGroupProperties

        [SerializeField] private RectOffset m_Padding = new();
        public RectOffset padding { get { return m_Padding; } set { m_Padding = value; UpdateItemData(); SetDirty(); } }

        [SerializeField] private float m_Spacing = 0;
        public float spacing { get { return m_Spacing; } set { m_Spacing = value; UpdateItemData(); SetDirty(); } }

        [SerializeField] private TextAnchor m_ChildAlignment = TextAnchor.MiddleCenter;
        public TextAnchor childAlignment { get { return m_ChildAlignment; } set { m_ChildAlignment = value; SetDirty(); } }

        [SerializeField] private bool m_ReverseArrangement = false;
        public bool reverseArrangement { get { return m_ReverseArrangement; } set { m_ReverseArrangement = value; UpdateItemData(); SetDirty(); } }

        [SerializeField] private bool m_ChildControl = true;
        public bool childControl { get { return m_ChildControl; } set { m_ChildControl = value; SetDirty(); } }

        [SerializeField] private bool m_ChildControlLayout = true;
        public bool childControlOther { get { return m_ChildControlLayout; } set { m_ChildControlLayout = value; SetDirty(); } }

        [SerializeField] private bool m_ChildScale = false;
        public bool childScale { get { return m_ChildScale; } set { m_ChildScale = value; SetDirty(); } }

        [SerializeField] protected bool m_ChildForceExpand = true;
        public bool childForceExpandWidth { get { return m_ChildForceExpand; } set { m_ChildForceExpand = value; SetDirty(); } }

        #endregion

        private void CalcAlongAxis(int axis)
        {
            bool alongOtherAxis = isVertical ^ (axis == 1);

            float combinedPadding = axis == 0 ? padding.horizontal : padding.vertical;
            bool controlSize = alongOtherAxis ? m_ChildControl : m_ChildControlLayout;
            bool childForceExpandSize = axis != (int)layoutAxis && m_ChildForceExpand;

            if (alongOtherAxis)
            {
                float totalMin = combinedPadding;
                float totalPreferred = combinedPadding;
                float totalFlexible = 0;
                for (int i = m_HeadIndex; i < m_TailIndex; i++)
                {
                    RectTransform child = m_ScrollItems[i].gameObject.transform as RectTransform;
                    GetChildSizes(child, axis, controlSize, childForceExpandSize, out float min, out float preferred, out float flexible);

                    if (m_ChildScale)
                    {
                        float scaleFactor = child.localScale[axis];
                        min *= scaleFactor;
                        preferred *= scaleFactor;
                        flexible *= scaleFactor;
                    }

                    if (alongOtherAxis)
                    {
                        totalMin = Mathf.Max(min + combinedPadding, totalMin);
                        totalPreferred = Mathf.Max(preferred + combinedPadding, totalPreferred);
                        totalFlexible = Mathf.Max(flexible, totalFlexible);
                    }
                    else
                    {
                        totalMin += min + spacing;
                        totalPreferred += preferred + spacing;

                        // Increment flexible size with element's flexible size.
                        totalFlexible += flexible;
                    }
                }

                m_TotalMinSize[axis] = totalMin;
                m_TotalPreferredSize[axis] = Mathf.Max(totalMin, totalPreferred);
                m_TotalFlexibleSize[axis] = totalFlexible;
            }
            else
            {
                m_TotalMinSize[axis] = 0;
                m_TotalFlexibleSize[axis] = 0;
            }
        }

        private void SetChildrenAlongAxis(int axis)
        {
            bool alongOtherAxis = isVertical ^ (axis == 1);

            float size = alongOtherAxis ? m_ParentSize[axis] : rectTransform.sizeDelta[axis];
            bool controlSize = alongOtherAxis ? m_ChildControl : m_ChildControlLayout;
            bool childForceExpandSize = axis != (int)layoutAxis && m_ChildForceExpand;
            float alignmentOnAxis = GetAlignmentOnAxis(axis);

            if (alongOtherAxis)
            {
                float innerSize = size - (axis == 0 ? padding.horizontal : padding.vertical);

                for (int i = m_HeadIndex; i < m_TailIndex; i++)
                {
                    RectTransform child = m_ScrollItems[i].gameObject.transform as RectTransform;
                    GetChildSizes(child, axis, controlSize, childForceExpandSize, out float min, out float preferred, out float flexible);
                    float scaleFactor = m_ChildScale ? child.localScale[axis] : 1f;

                    float requiredSpace = Mathf.Clamp(innerSize, min, flexible > 0 ? size : preferred);
                    float startOffset = GetStartOffset(axis, requiredSpace * scaleFactor);
                    if (controlSize)
                    {
                        SetChildAlongAxisWithScale(child, axis, startOffset, requiredSpace, scaleFactor);
                    }
                    else
                    {
                        float offsetInCell = (requiredSpace - child.sizeDelta[axis]) * alignmentOnAxis;
                        SetChildAlongAxisWithScale(child, axis, startOffset + offsetInCell, scaleFactor);
                    }
                }
            }
            else
            {
                for (int i = m_HeadIndex; i < m_TailIndex; i++)
                {
                    var item = m_ScrollItems[i];
                    var child = item.gameObject.transform as RectTransform;

                    if (controlSize)
                    {
                        SetChildAlongAxisWithScale(child, axis, item.position, item.size, 1);
                    }
                    else
                    {
                        SetChildAlongAxisWithScale(child, axis, item.position, 1);
                    }
                }
            }
        }

        private void SetChildAlongAxisWithScale(RectTransform rect, int axis, float pos, float size, float scaleFactor)
        {
            if (!rect) return;

            m_Tracker.Add(this, rect,
                DrivenTransformProperties.Anchors |
                (axis == 0 ?
                    (DrivenTransformProperties.AnchoredPositionX | DrivenTransformProperties.SizeDeltaX) :
                    (DrivenTransformProperties.AnchoredPositionY | DrivenTransformProperties.SizeDeltaY)
                )
            );

            rect.anchorMin = rect.anchorMax = Vector2.up;

            Vector2 sizeDelta = rect.sizeDelta;
            sizeDelta[axis] = size;
            rect.sizeDelta = sizeDelta;

            Vector2 anchoredPosition = rect.anchoredPosition;
            anchoredPosition[axis] = axis == 0
                ? (pos + size * rect.pivot[axis] * scaleFactor)
                : (-pos - size * (1f - rect.pivot[axis]) * scaleFactor);
            rect.anchoredPosition = anchoredPosition;
        }

        private void SetChildAlongAxisWithScale(RectTransform rect, int axis, float pos, float scaleFactor)
        {
            if (!rect) return;

            m_Tracker.Add(this, rect,
                DrivenTransformProperties.Anchors |
                (axis == 0 ? DrivenTransformProperties.AnchoredPositionX : DrivenTransformProperties.AnchoredPositionY)
            );

            rect.anchorMin = rect.anchorMax = Vector2.up;

            Vector2 anchoredPosition = rect.anchoredPosition;
            anchoredPosition[axis] = axis == 0
                ? (pos + rect.sizeDelta[axis] * rect.pivot[axis] * scaleFactor)
                : (-pos - rect.sizeDelta[axis] * (1f - rect.pivot[axis]) * scaleFactor);
            rect.anchoredPosition = anchoredPosition;
        }

        private float GetStartOffset(int axis, float requiredSpaceWithoutPadding)
        {
            float requiredSpace = requiredSpaceWithoutPadding + (axis == 0 ? padding.horizontal : padding.vertical);
            float availableSpace = isVertical ^ (axis == 1) ? m_ParentSize[axis] : rectTransform.sizeDelta[axis];
            float surplusSpace = availableSpace - requiredSpace;
            float alignmentOnAxis = GetAlignmentOnAxis(axis);
            return (axis == 0 ? padding.left : padding.top) + surplusSpace * alignmentOnAxis;
        }

        private void GetChildSizes(RectTransform child, int axis, bool controlSize, bool childForceExpand,
            out float min, out float preferred, out float flexible)
        {
            if (controlSize)
            {
                min = LayoutUtility.GetMinSize(child, axis);
                preferred = LayoutUtility.GetPreferredSize(child, axis);
                flexible = LayoutUtility.GetFlexibleSize(child, axis);
            }
            else
            {
                min = child.sizeDelta[axis];
                preferred = min;
                flexible = 0;
            }

            if (childForceExpand) flexible = Mathf.Max(flexible, 1);
        }

        protected float GetAlignmentOnAxis(int axis)
        {
            if (axis == 0)
                return (int)childAlignment % 3 * 0.5f;
            else
                return (int)childAlignment / 3 * 0.5f;
        }

        #endregion

        #region Dirty

        [NonSerialized] private bool m_IsDirty;

        private void SetDirty()
        {
            if (!IsActive() || m_IsDirty) return;
            m_IsDirty = true;

            if (CanvasUpdateRegistry.IsRebuildingLayout())
                StartCoroutine(DelayedSetDirty(rectTransform));
            else
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        private IEnumerator DelayedSetDirty(RectTransform rectTransform)
        {
            yield return null;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        #endregion

        #region Editor
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            UpdateItemData();
            SetDirty();
        }

        protected override void Reset()
        {
            m_ChildControl = false;
            m_ChildControlLayout = false;
            rectTransform.anchoredPosition = Vector2.zero;
            SetDirty();
        }

        [NonSerialized] private readonly List<Vector2> m_Sizes = new List<Vector2>(10);
        protected virtual void Update()
        {
            if (Application.isPlaying) return;

            var dirty = false;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (m_Sizes.Count <= i) m_Sizes.Add(Vector2.zero);

                var t = transform.GetChild(i) as RectTransform;
                if (t && t.sizeDelta != m_Sizes[i])
                {
                    m_Sizes[i] = t.sizeDelta;
                    dirty = true;
                }
            }

            if (dirty) SetDirty();
        }
#endif
        #endregion
    }
}
