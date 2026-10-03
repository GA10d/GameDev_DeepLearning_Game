using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using LearningFoundry.Core;
using LearningFoundry.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LearningFoundry.Game
{
    public sealed partial class FoundryGame
    {
        IEnumerator CampaignSmoke()
        {
            yield return null;var args=Environment.GetCommandLineArgs();string fixture=null,output=null;for(int i=0;i<args.Length-1;i++){if(args[i]=="--fixture")fixture=args[i+1];if(args[i]=="--capture-output")output=args[i+1];}output=output??"campaign-smoke";Directory.CreateDirectory(output);var report=new CampaignSmokeReport{passed=true};
            try{profile=JsonUtility.FromJson<Profile>(File.ReadAllText(fixture));profile.onboardingVersion=1;profile.completed=Missions.Implemented.ToList();}
            catch(Exception e){Debug.LogException(e);Application.Quit(2);yield break;}
            foreach(string id in LabMissions.Ids){
                try{OpenLevel(id);labSelection=-1;LabObserve();if(!LabData.IsClassifier(id)&&id!="S02"){labSelection=0;LabGradient();}if(id=="E06"){lab.partition=1;LabObserve();}LabVisualize();report.ids.Add(id);var verdict=LabMissions.Check(lab);if(!verdict.passed)throw new Exception(id+" fixture no longer passes: "+verdict.message+" / frozen="+lab.frozenHash+" actual="+ModelIO.Hash(lab.layers));}
                catch(Exception e){report.passed=false;report.errors.Add(id+": "+e.Message);Debug.LogException(e);}
                yield return null;yield return new WaitForEndOfFrame();CaptureLab(Path.Combine(output,id+".png"));
            }
            // Exercise actual native controls instead of only opening prepared screens.
            OpenLevel("C02");lab.layers.Clear();lab.actions=0;lab.observed.Clear();LabAdd(TensorOp.Gate);LabObserve();labSample=1;LabObserve();report.addAndObserve=LabMissions.Check(lab).passed;
            OpenLevel("D02");lab.layers[0].reshape=new[]{5};LabObserve();report.invalidShapeFeedback=lab.signatures.Contains("invalid-shape");lab.layers[0].reshape=new[]{6};LabObserve();report.shapeRepair=LabMissions.Check(lab).passed;
            OpenLevel("F09");var original=lab.layers[0];lab.layers=ModelIO.Copy(original.children);labSelection=-1;DrawLabBench();LabPackage();LabCloneExperiment();LabGradient();report.packAndIndependentCopy=LabMissions.Check(lab).passed;
            OpenLevel("G04");LabFreeze();report.freezeControl=lab.tested&&lab.frozenHash==ModelIO.Hash(lab.layers);CloseCurrentOverlay();LabDrawing();yield return null;
            var pad=activeOverlay.GetComponentInChildren<DigitPad>();Array.Clear(pad.pixels,0,784);var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerPressRaycast=new RaycastResult{module=canvas.GetComponent<GraphicRaycaster>()}};
            pointer.position=RectTransformUtility.WorldToScreenPoint(uiCamera,pad.rectTransform.TransformPoint(new Vector3(150,-45,0)));pad.OnPointerDown(pointer);
            pointer.position=RectTransformUtility.WorldToScreenPoint(uiCamera,pad.rectTransform.TransformPoint(new Vector3(145,-230,0)));pad.OnDrag(pointer);pad.OnPointerUp(pointer);
            var infer=activeOverlay.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>()?.text=="推理这张图");lab.actions&=~LabMissions.Drawn;infer.onClick.Invoke();report.drawAndInfer=pad.pixels.Sum()>1&&(lab.actions&LabMissions.Drawn)!=0;
            yield return null;yield return new WaitForEndOfFrame();CaptureLab(Path.Combine(output,"G04-drawing.png"));CloseCurrentOverlay();LabExport();report.modelExport=Directory.GetFiles(Path.Combine(output,"exports"),"*.json").Length>0;CloseCurrentOverlay();
            OpenLevel("G03");lab.tested=false;int previous=lab.steps;labRunning=true;yield return null;yield return null;labRunning=false;report.playerTraining=lab.steps>previous;report.passed&=report.addAndObserve&&report.invalidShapeFeedback&&report.shapeRepair&&report.packAndIndependentCopy&&report.playerTraining;
            report.passed&=report.freezeControl&&report.drawAndInfer&&report.modelExport;
            File.WriteAllText(Path.Combine(output,"campaign-player-smoke.json"),JsonUtility.ToJson(report,true));Debug.Log("CAMPAIGN_PLAYER_SMOKE "+report.passed+" / "+report.ids.Count+" level screens");Application.Quit(report.passed?0:2);
        }
        void CaptureLab(string path){Canvas.ForceUpdateCanvases();var render=new RenderTexture(1600,900,24);render.Create();var old=uiCamera.targetTexture;var previous=RenderTexture.active;uiCamera.targetTexture=render;uiCamera.Render();RenderTexture.active=render;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());uiCamera.targetTexture=old;RenderTexture.active=previous;render.Release();Destroy(render);Destroy(image);}
        [Serializable]sealed class CampaignSmokeReport{public bool passed,addAndObserve,invalidShapeFeedback,shapeRepair,packAndIndependentCopy,playerTraining,freezeControl,drawAndInfer,modelExport;public List<string> ids=new List<string>(),errors=new List<string>();}
    }
}
