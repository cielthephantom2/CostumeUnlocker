namespace Il2CppSystem {public class Action{}}
namespace Il2CppSystem.Collections.Generic{public class List<T>:System.Collections.Generic.List<T>{}}
namespace UnityEngine
{
 public class Component
 {
  static int next;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref next);public GameObject gameObject;
  public Transform transform=>gameObject.transform;
  public T GetComponent<T>()where T:Component=>gameObject.GetComponent<T>();
  public T[] GetComponentsInChildren<T>(bool inactive)where T:Component=>gameObject.All().SelectMany(g=>g.Components).OfType<T>().ToArray();
 }
 public class Transform:Component
 {
  public Transform parent;public List<Transform> children=new();
  public int GetSiblingIndex()=>parent.children.IndexOf(this);
  public Transform GetChild(int index)=>children[index];
 }
 public class RectTransform:Transform
 {
  public Rect rect=>new(width,height);
  public enum Axis{Horizontal,Vertical}public float width=25,height=40;
  public void SetSizeWithCurrentAnchors(Axis axis,float size){if(axis==Axis.Horizontal)width=size;else height=size;}
 }
 public struct Rect(float width,float height){public float width=width,height=height;}
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public class GameObject
 {
  public string name;public bool activeSelf=true;public bool activeInHierarchy=>activeSelf && (transform.parent?.gameObject.activeInHierarchy??true);
  public readonly List<Component> Components=new();public readonly RectTransform transform;
  public GameObject(string label){name=label;transform=AddComponent<RectTransform>();}
  public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
  public T GetComponent<T>()where T:Component=>Components.OfType<T>().FirstOrDefault();
  public IEnumerable<GameObject> All()=>new[]{this}.Concat(transform.children.SelectMany(t=>t.gameObject.All()));
  public void Child(GameObject child){child.transform.parent=transform;transform.children.Add(child.transform);}
  public T[] GetComponentsInChildren<T>(bool inactive)where T:Component=>All().SelectMany(g=>g.Components).OfType<T>().ToArray();
 }
 public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
}
namespace Il2CppTMPro
{
 public class TMP_Text:UnityEngine.Component
 {
  public bool enableWordWrapping=true;public int maxVisibleCharacters=2,maxVisibleWords=1,maxVisibleLines=1;
  public string text;public float fontScale=1;
  public UnityEngine.RectTransform rectTransform=>gameObject.transform;
  public UnityEngine.Vector2 GetPreferredValues(string value)=>new(value.Sum(c=>c=='i'?4.25f:10.5f)*fontScale,20);
  public UnityEngine.Vector2 GetPreferredValues(string value,float width,float height)=>GetPreferredValues(value);
 }
}
namespace UnityEngine.UI
{
 public class Graphic:UnityEngine.Component{public UnityEngine.Color color;}
 public class Selectable:UnityEngine.Component{public enum Transition{None,ColorTint}public Transition transition=Transition.ColorTint;public Graphic targetGraphic;}
 public class Button:Selectable{}
 public class ContentSizeFitter:UnityEngine.Component{public bool ResetWidth;public static List<string> Calls=new();public void SetLayoutHorizontal(){Calls.Add(gameObject.name+":H");if(ResetWidth)gameObject.transform.width=25;}public void SetLayoutVertical()=>Calls.Add(gameObject.name+":V");}
}
namespace Il2Cpp
{
 public class SimpleLayoutGroup:UnityEngine.Component
 {
  public static List<string> Calls=new();
  public UnityEngine.RectTransform _rectTransform;public int _layoutMode,_childAlignment,_updateMode;public object _padding;
  public float _spacing,_minWidth,_minHeight,_preferredWidth,_preferredHeight;public bool _adaptHorizontalSize,_adaptVerticalSize;
  public Il2CppSystem.Action ElementsChanged;
  public Il2CppSystem.Collections.Generic.List<SimpleLayoutElement> Elements=new(),_elementsCache=new();
  public void UpdateLayout(){Calls.Add(gameObject.name);foreach(var element in Elements)if(element.ParentControlsSize)element.RectTransformReference.width=25;}
  public class SimpleLayoutElement
  {
   public SimpleLayoutElement(SimpleLayoutGroup group,UnityEngine.RectTransform rect,bool parentControls,bool expand){LayoutGroupReference=group;RectTransformReference=rect;ParentControlsSize=parentControls;ExpandAcrossAxis=expand;}
   public SimpleLayoutGroup LayoutGroupReference;public UnityEngine.RectTransform RectTransformReference;public bool ParentControlsSize,ExpandAcrossAxis,_excludeIfInactive,Ignored;
   public bool _parentControlsSize{get=>ParentControlsSize;set=>ParentControlsSize=value;}public bool _expandAcrossAxis{get=>ExpandAcrossAxis;set=>ExpandAcrossAxis=value;}
  }
 }
}
