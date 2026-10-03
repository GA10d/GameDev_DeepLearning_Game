using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LearningFoundry.Core;

namespace LearningFoundry.UI
{
    public sealed class TensorGrid : MaskableGraphic, IPointerClickHandler, IPointerMoveHandler
    {
        public float[] values=new float[0]; public int columns=1, offset, count, selected=-1; public int[] highlighted; public bool grayscale; public Action<int> inspect;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(values==null||values.Length==0||count==0)return;int rows=(count+columns-1)/columns;float w=rectTransform.rect.width/columns,h=rectTransform.rect.height/rows;float max=0.0001f;
            for(int i=0;i<count;i++)max=Math.Max(max,Math.Abs(values[(offset+i)%values.Length]));
            for(int i=0;i<count;i++){float v=values[(offset+i)%values.Length],t=Math.Min(1,Math.Abs(v)/max);Color c=grayscale?new Color(t*.77f+.06f,t*.87f+.08f,t*.72f+.07f):v<0?Color.Lerp(Hardware.Dark,new Color(.9f,.45f,.24f),t):Color.Lerp(Hardware.Dark,Hardware.Phosphor,t);if(i==selected)c=Style.Paper;
                if(highlighted!=null&&Array.IndexOf(highlighted,i)>=0)c=Color.Lerp(c,new Color(.94f,.66f,.22f),.6f);
                float x=(i%columns)*w,y=-(i/columns)*h,gap=count>300?0:.7f;int at=vh.currentVertCount;vh.AddVert(new Vector3(x,y),c,Vector2.zero);vh.AddVert(new Vector3(x+w-gap,y),c,Vector2.zero);vh.AddVert(new Vector3(x+w-gap,y-h+gap),c,Vector2.zero);vh.AddVert(new Vector3(x,y-h+gap),c,Vector2.zero);vh.AddTriangle(at,at+1,at+2);vh.AddTriangle(at,at+2,at+3);}
        }
        public void Set(float[] data,int cols,int start=0,int length=0,bool gray=false){values=data;columns=Math.Max(1,cols);offset=start;count=length==0?data.Length:length;grayscale=gray;SetVerticesDirty();}
        int Index(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var p);int rows=(count+columns-1)/columns;return Mathf.Clamp((int)(-p.y/rectTransform.rect.height*rows)*columns+(int)(p.x/rectTransform.rect.width*columns),0,count-1);}
        public void OnPointerClick(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left||count==0)return;selected=Index(e);inspect?.Invoke(offset+selected);SetVerticesDirty();}
        public void OnPointerMove(PointerEventData e){if(count>0)inspect?.Invoke(offset+Index(e));}
    }
    public sealed class DigitPad : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float[] pixels=new float[784];public Action changed;Vector2 last;bool drawing;
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();float w=rectTransform.rect.width/28,h=rectTransform.rect.height/28;for(int i=0;i<784;i++){Color c=Color.Lerp(Hardware.Dark,Hardware.Phosphor,pixels[i]);float x=i%28*w,y=-(i/28)*h;int a=vh.currentVertCount;vh.AddVert(new Vector3(x,y),c,Vector2.zero);vh.AddVert(new Vector3(x+w,y),c,Vector2.zero);vh.AddVert(new Vector3(x+w,y-h),c,Vector2.zero);vh.AddVert(new Vector3(x,y-h),c,Vector2.zero);vh.AddTriangle(a,a+1,a+2);vh.AddTriangle(a,a+2,a+3);}}
        Vector2 Point(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var p);return new Vector2(p.x/rectTransform.rect.width*28,-p.y/rectTransform.rect.height*28);}
        void Stroke(Vector2 point){int steps=Math.Max(1,(int)((point-last).magnitude*3));for(int s=0;s<=steps;s++){Vector2 p=Vector2.Lerp(last,point,s/(float)steps);for(int y=Math.Max(0,(int)p.y-2);y<Math.Min(28,(int)p.y+3);y++)for(int x=Math.Max(0,(int)p.x-2);x<Math.Min(28,(int)p.x+3);x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),p);pixels[y*28+x]=Math.Max(pixels[y*28+x],Mathf.Clamp01(1.65f-d));}}last=point;SetVerticesDirty();changed?.Invoke();}
        public void OnPointerDown(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;drawing=true;last=Point(e);Stroke(last);}
        public void OnDrag(PointerEventData e){if(drawing&&e.button==PointerEventData.InputButton.Left)Stroke(Point(e));}
        public void OnPointerUp(PointerEventData e){drawing=false;}
        // Matches MNIST framing: crop ink to a 20-pixel box, preserve aspect, center of mass at (13.5,13.5).
        public static float[] Adapt(float[] input)
        {
            int minX=28,minY=28,maxX=-1,maxY=-1;for(int y=0;y<28;y++)for(int x=0;x<28;x++)if(input[y*28+x]>.1f){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}var output=new float[784];if(maxX<0)return output;
            float scale=20f/Math.Max(maxX-minX+1,maxY-minY+1);int w=Math.Max(1,(int)((maxX-minX+1)*scale)),h=Math.Max(1,(int)((maxY-minY+1)*scale));var temp=new float[784];int left=(28-w)/2,top=(28-h)/2;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){float sx=minX+(x+.5f)/scale-.5f,sy=minY+(y+.5f)/scale-.5f;int ax=Mathf.Clamp((int)Math.Floor(sx),0,27),ay=Mathf.Clamp((int)Math.Floor(sy),0,27),bx=Math.Min(27,ax+1),by=Math.Min(27,ay+1);float fx=Mathf.Clamp01(sx-ax),fy=Mathf.Clamp01(sy-ay);temp[(top+y)*28+left+x]=Mathf.Lerp(Mathf.Lerp(input[ay*28+ax],input[ay*28+bx],fx),Mathf.Lerp(input[by*28+ax],input[by*28+bx],fx),fy);}
            double total=0,cx=0,cy=0;for(int i=0;i<784;i++){total+=temp[i];cx+=temp[i]*(i%28);cy+=temp[i]*(i/28);}int dx=total>0?(int)Math.Round(13.5-cx/total):0,dy=total>0?(int)Math.Round(13.5-cy/total):0;
            for(int y=0;y<28;y++)for(int x=0;x<28;x++){int sx=x-dx,sy=y-dy;if(sx>=0&&sx<28&&sy>=0&&sy<28)output[y*28+x]=temp[sy*28+sx];}return output;
        }
    }
}
