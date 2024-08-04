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

        private struct ScrollItem
        {
            private int m_PrefabID;
            private GameObject m_Prefab;

            public float position { get; set; }
            public float size { get; set; }
            public readonly int prefabID => m_PrefabID;
            public GameObject prefab { readonly get { return m_Prefab; } set { m_Prefab = value; m_PrefabID = m_Prefab ? m_Prefab.GetInstanceID() : 0; } }
            public GameObject gameObject { get; private set; }

            public readonly bool IsVisible(float viewHead, float viewTail)
            {
                var posTail = position + size;
                return position >= viewHead && position < viewTail || posTail > viewHead && posTail <= viewTail;
            }

            public bool ShowItem(Dictionary<int, Queue<GameObject>> pools, Transform parent, Action<int, GameObject> onUpdateItem, int updateIndex)
            {
                if (gameObject) return false;

                if (!pools.TryGetValue(prefabID, out var itemPool)) pools[prefabID] = itemPool = new();
                gameObject = itemPool.Count > 0 ? itemPool.Dequeue() : Instantiate(prefab, parent);
                onUpdateItem?.Invoke(updateIndex, gameObject);
                gameObject.SetActive(true);

                return true;
            }

            public bool HideItem(Dictionary<int, Queue<GameObject>> pools)
            {
                if (!gameObject) return false;

                gameObject.SetActive(false);
                if (pools.TryGetValue(prefabID, out var itemPool)) itemPool.Enqueue(gameObject);
                else Destroy(gameObject);
                gameObject = null;

                return true;
            }
        }

        [SerializeField] private RectTransform m_Viewport;
        public RectTransform viewport { get { return m_Viewport ? m_Viewport : transform.parent is RectTransform parentRect ? parentRect : null; } set { m_Viewport = value; UpdateItem(true, true, false); } }

        [SerializeField] private RectTransform.Axis m_LayoutAxis = RectTransform.Axis.Vertical;
        public RectTransform.Axis layoutAxis { get { return m_LayoutAxis; } set { m_LayoutAxis = value; UpdateItemData(true); } }

        private bool isVertical => layoutAxis == RectTransform.Axis.Vertical;

        [SerializeField] private ListAnchor m_Alignment = ListAnchor.MiddleCenter;
        public ListAnchor alignment { get { return m_Alignment; } set { m_Alignment = value; UpdateItem(true, true, false); } }

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
            UpdateItemData(true);
        }

        protected override void OnDisable()
        {
            m_Tracker.Clear();
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        protected virtual void LateUpdate()
        {
            var viewSizeChange = CheckViewport();
            var ancPosChange = CheckAnchoredPos();

            if (viewSizeChange || ancPosChange)
            {
                UpdateItem(false, viewSizeChange, false);
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

        [NonSerialized] private Vector2 m_ViewPos;
        [NonSerialized] private Vector2 m_ViewSize;
        [NonSerialized] private Vector2 m_ParentSize;
        [NonSerialized] private Vector2 m_SelfPos;
        private void UpdateCheckValue()
        {
            var viewport = this.viewport;
            var pos = viewport ? viewport.anchoredPosition : Vector2.zero;
            var size = viewport ? viewport.rect.size : Vector2.zero;
            m_ViewPos = pos;
            m_ViewSize = size;

            if (viewport == transform.parent)
                m_ParentSize = m_ViewSize;
            else
                m_ParentSize = transform.parent is RectTransform parentRect ? parentRect.rect.size : Vector2.zero;

            m_SelfPos = rectTransform.anchoredPosition;
        }

        private bool CheckViewport()
        {
            var isChange = false;
            var viewport = this.viewport;
            var viewPos = viewport ? viewport.anchoredPosition : Vector2.zero;
            var viewSize = viewport ? viewport.rect.size : Vector2.zero;
            if (m_ViewPos != viewPos)
            {
                m_ViewPos = viewPos;
                isChange = true;
            }
            if (m_ViewSize != viewSize)
            {
                m_ViewSize = viewSize;
                isChange = true;
            }
            if (viewport == transform.parent)
            {
                m_ParentSize = m_ViewSize;
            }
            else
            {
                var parentSize = transform.parent is RectTransform parentRect ? parentRect.rect.size : Vector2.zero;
                if (m_ParentSize != parentSize)
                {
                    m_ParentSize = parentSize;
                    isChange = true;
                }
            }
            return isChange;
        }

        private bool CheckAnchoredPos()
        {
            var isChange = false;
            var selfPos = rectTransform.anchoredPosition;
            if (m_SelfPos != selfPos)
            {
                m_SelfPos = selfPos;
                isChange = true;
            }
            return isChange;
        }

        #endregion

        #region LayoutElement

        public virtual void CalculateLayoutInputHorizontal()
        {
            m_IsDirty = false;
            m_Tracker.Clear();

            m_Tracker.Add(this, rectTransform,
                DrivenTransformProperties.SizeDelta | DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot
            );

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
            SetChildrenAlongAxis(0);
        }

        public virtual void SetLayoutVertical()
        {
            SetChildrenAlongAxis(1);
        }

        #endregion

        #region ScrollItems

        public void SetItemData(int itemCount, Func<int, float> onGetItemSize, Func<int, GameObject> onGetPrefab, Action<int, GameObject> onUpdateItem, bool resetPos = true, bool clearPools = true)
        {
            m_ItemCount = Math.Max(itemCount, 0);
            m_OnGetItemSize = onGetItemSize;
            m_OnGetPrefab = onGetPrefab;
            m_OnUpdateItem = onUpdateItem;

            UpdateItemData(resetPos, true, true);
            if (clearPools) ClearPools();
        }

        public void UpdateItemData(bool resetPos = false, bool updateSize = false, bool updatePrefab = false)
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
                    item.HideItem(m_PrefabPools);
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
                    if (item.prefab != nextPrefab) item.HideItem(m_PrefabPools);
                    item.prefab = nextPrefab;
                }
                m_ScrollItems[i] = item;

                pos += item.size + spacing;
            }

            m_TotalPreferredSize[axis] = pos - (m_ItemCount > 0 ? spacing : 0) + (axis == 0 ? padding.right : padding.bottom);
            if (isVertical)
                rectTransform.sizeDelta = new Vector2(0, m_TotalPreferredSize.y);
            else
                rectTransform.sizeDelta = new Vector2(m_TotalPreferredSize.x, 0);

            UpdateItem(true, true, resetPos);
        }

        public void ResetPos()
        {
            UpdateItem(true, false, true);
        }

        public void ClearPools()
        {
            foreach (var pool in m_PrefabPools.Values)
            {
                while (pool.Count > 0) Destroy(pool.Dequeue());
            }
            m_PrefabPools.Clear();
        }

        [NonSerialized] private readonly Vector3[] m_ViewCorners = new Vector3[4];
        private void UpdateItem(bool updateCheckValue, bool forceDirty, bool resetPos)
        {
            if (updateCheckValue) UpdateCheckValue();
            if (!viewport) return;

            SetSelfRect(resetPos);

            var axis = (int)layoutAxis;

            viewport.GetWorldCorners(m_ViewCorners);
            var topLeftWorld = m_ViewCorners[1];
            var bottomRightWorld = m_ViewCorners[3];
            var headPos = rectTransform.InverseTransformPoint(topLeftWorld)[axis];
            var tailPos = rectTransform.InverseTransformPoint(bottomRightWorld)[axis];
            if (isVertical)
            {
                var viewSize = headPos - tailPos;
                headPos = (1 - rectTransform.pivot[axis]) * rectTransform.sizeDelta[axis] - headPos;
                tailPos = headPos + viewSize;
            }
            else
            {
                var viewSize = tailPos - headPos;
                headPos = rectTransform.pivot[axis] * rectTransform.sizeDelta[axis] + headPos;
                tailPos = headPos + viewSize;
            }

            int curIndex;
            var isDirty = false;

            for (curIndex = 0; curIndex < m_ItemCount; curIndex++)
            {
                var item = m_ScrollItems[curIndex];
                if (!item.IsVisible(headPos, tailPos))
                {
                    if (item.HideItem(m_PrefabPools)) isDirty = true;
                    m_ScrollItems[curIndex] = item;
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
                if (item.HideItem(m_PrefabPools)) isDirty = true;
                m_ScrollItems[curIndex] = item;
            }
            for (int i = m_HeadIndex; i < m_TailIndex; i++)
            {
                var item = m_ScrollItems[i];
                if (item.ShowItem(m_PrefabPools, transform, m_OnUpdateItem, i)) isDirty = true;
                m_ScrollItems[i] = item;
            }

            if (isDirty || forceDirty) SetDirty();
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

        #endregion

        #region LayoutGroup

        #region LayoutGroupProperties

        [SerializeField] private RectOffset m_Padding = new();
        public RectOffset padding { get { return m_Padding; } set { m_Padding = value; UpdateItemData(); } }

        [SerializeField] private float m_Spacing = 0;
        public float spacing { get { return m_Spacing; } set { m_Spacing = value; UpdateItemData(); } }

        [SerializeField] private TextAnchor m_ChildAlignment = TextAnchor.MiddleCenter;
        public TextAnchor childAlignment { get { return m_ChildAlignment; } set { m_ChildAlignment = value; SetDirty(); } }

        [SerializeField] private bool m_ReverseArrangement = false;
        public bool reverseArrangement { get { return m_ReverseArrangement; } set { m_ReverseArrangement = value; UpdateItemData(); } }

        [SerializeField] private bool m_ChildControl = true;
        public bool childControl { get { return m_ChildControl; } set { m_ChildControl = value; SetDirty(); } }

        [SerializeField] private bool m_ChildControlLayout = true;
        public bool childControlOther { get { return m_ChildControlLayout; } set { m_ChildControlLayout = value; SetDirty(); } }

        [SerializeField] private bool m_ChildScale = false;
        public bool childScale { get { return m_ChildScale; } set { m_ChildScale = value; SetDirty(); } }

        [SerializeField] protected bool m_ChildForceExpand = true;
        public bool childForceExpandWidth { get { return m_ChildForceExpand; } set { m_ChildForceExpand = value; SetDirty(); } }

        [SerializeField] protected bool m_ChildForceExpandLayout = true;
        public bool childForceExpandWidthLayout { get { return m_ChildForceExpandLayout; } set { m_ChildForceExpandLayout = value; SetDirty(); } }

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
            bool childForceExpandSize = alongOtherAxis ? m_ChildForceExpand : m_ChildForceExpandLayout;
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
                        var childSize = childForceExpandSize ? item.size : Mathf.Min(item.size, LayoutUtility.GetPreferredSize(child, axis));
                        childSize = Mathf.Max(LayoutUtility.GetMinSize(child, axis), childSize);
                        SetChildAlongAxisWithScale(child, axis, item.position, childSize, 1);
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
        [NonSerialized] private RectTransform.Axis prevLayoutAxis;
        protected override void Awake()
        {
            prevLayoutAxis = layoutAxis;
        }

        [NonSerialized] private bool inspectorChange = false;
        protected override void OnValidate()
        {
            inspectorChange = true;
        }

        protected override void Reset()
        {
            m_ChildControl = false;
            m_ChildControlLayout = false;
            UpdateItemData(true);
        }

        [NonSerialized] private readonly List<Vector2> m_Sizes = new List<Vector2>(10);
        protected virtual void Update()
        {
            if (inspectorChange)
            {
                inspectorChange = false;
                UpdateItemData(prevLayoutAxis != layoutAxis);
                prevLayoutAxis = layoutAxis;
            }

            if (!Application.isPlaying)
            {
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
        }
#endif
        #endregion
    }
}
