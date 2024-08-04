# Unity UI EX
UGUI 扩展组件

个人对Unity UGUI的扩展组件。
目前只有ScrollList和ScrollContent扩展组件。

## ScrollList
这是一个使用了对象池用于滚动界面的列表组件，只会渲染在视窗范围内的子物体。
它结合了UGUI的LayoutGroup，在一些使用场景中可以自适应。
可以嵌套使用，指定相同的视窗范围即可。

## ScrollContent
这是一个继承ScrollList的组件，在ScrollList的基础上它会改变自身的大小和坐标来匹配父节点，以此来自适应滚动列表的视窗。
*换言之ScrollList只会控制子物体，而ScrollContent除此之外还会控制自身（同时有LayoutGroup和LayoutSelfController的逻辑）。

具体使用方法请查看样例。