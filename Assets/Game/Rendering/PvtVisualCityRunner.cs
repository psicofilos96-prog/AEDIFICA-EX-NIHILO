using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Visual approval scene, separate from every performance benchmark. GUI commands
    // exercise the same world/history/combined-page APIs without an indivisible prefab.
    public sealed class PvtVisualCityRunner : MonoBehaviour
    {
        private static readonly string[] Views = { "facade", "materials", "street", "landscape", "monument" };
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Material stone,brick,wood,plaster,roof,road,terrain,water,glass;
        [SerializeField] private Texture2D groundAlbedo,groundNormal,groundMask;
        [SerializeField] private GameObject pinePrefab,shrubPrefab,doorPrefab,gatePrefab,statuePrefab;

        private ConstructionWorld world;
        private ConstructionCommandHistory history;
        private MaterialRegistry registry;
        private PvtChunkVisualEngine engine;
        private PvtVisualEnvironment environment;
        private GameObject visualRoot, door, gate, statue;
        private PieceId selected, doorHost;
        private bool hasSelection, hideUi;
        private int viewIndex;
        private string message="Click a piece in Game view to select it.";
        private string SavePath => Path.Combine(Application.persistentDataPath,"aedifica_visual_city.pvt2");

        public ConstructionWorld World => world;
        public PvtChunkVisualEngine VisualEngine => engine;

        public void Configure(Camera camera,Material stoneMaterial,Material brickMaterial,
            Material woodMaterial,Material plasterMaterial,Material roofMaterial,Material roadMaterial,
            Material terrainMaterial,Material waterMaterial,Material glassMaterial,Texture2D albedo,Texture2D normal,
            Texture2D mask,GameObject pine,GameObject shrub,GameObject doorModel,
            GameObject gateModel,GameObject statueModel)
        {
            sceneCamera=camera;stone=stoneMaterial;brick=brickMaterial;wood=woodMaterial;
            plaster=plasterMaterial;roof=roofMaterial;road=roadMaterial;terrain=terrainMaterial;
            water=waterMaterial;glass=glassMaterial;
            groundAlbedo=albedo;groundNormal=normal;groundMask=mask;
            pinePrefab=pine;shrubPrefab=shrub;doorPrefab=doorModel;gatePrefab=gateModel;
            statuePrefab=statueModel;
        }

        private void Start()
        {
            if (sceneCamera==null || stone==null || brick==null || wood==null ||
                plaster==null || roof==null || road==null || terrain==null || water==null || glass==null ||
                groundAlbedo==null || groundNormal==null || groundMask==null ||
                pinePrefab==null || shrubPrefab==null || doorPrefab==null ||
                gatePrefab==null || statuePrefab==null)
            { Debug.LogError("PVT Visual City has missing serialized art references.",this);enabled=false;return; }
            registry=new MaterialRegistry(stone,stone,brick,plaster);
            registry.Register(PvtFinalScenario.Wood,wood);
            registry.Register(PvtFinalScenario.Ceramic,roof);
            registry.Register(PvtVisualCityScenario.Road,road);
            registry.Register(PvtVisualCityScenario.Glass,glass);
            environment=new PvtVisualEnvironment(transform,PvtVisualCityScenario.Seed,terrain,road,
                stone,water,groundAlbedo,groundNormal,groundMask,pinePrefab,shrubPrefab);
            BuildWorld(PvtVisualCityScenario.Build());
            PositionCamera(0);
            environment.UpdateStreaming(sceneCamera.transform.position);
            CreateProps();
            Debug.Log($"PVT Visual City ready: {world.Count} editable pieces, " +
                $"{PvtVisualCityScenario.BuildingCount} buildings. Visual quality awaits human approval.",this);
        }

        private void BuildWorld(IEnumerable<PieceData> pieces)
        {
            ClearSelection();
            if (engine!=null) engine.Dispose();
            if (visualRoot!=null) Destroy(visualRoot);
            world=new ConstructionWorld();
            foreach (PieceData piece in pieces)
                if (!world.Create(piece).Changed) throw new InvalidOperationException("Duplicate city PieceId.");
            history=new ConstructionCommandHistory(world,512);
            visualRoot=new GameObject("Individually editable architecture");
            visualRoot.transform.SetParent(transform,false);
            engine=new PvtChunkVisualEngine(world,registry,visualRoot.transform);
            engine.RebuildDirty();
            doorHost=default;
            foreach (PieceData piece in world.Pieces)
                if (piece.Type==PieceType.Wall && piece.Openings.Count>0 &&
                    Vector3.Distance(piece.Transform.Position,
                        new Vector3(-220f,Pvt2Scenario.GroundHeight(-220f,78f)+0.45f,69.5f))<1f)
                { doorHost=piece.Id;break; }
            world.Changed+=OnWorldChanged;
        }

        private void OnWorldChanged(ConstructionChangeSet change)
        {
            if (change.PieceId==doorHost) PositionDoor();
        }

        private void CreateProps()
        {
            door=Instantiate(doorPrefab,transform);
            gate=Instantiate(gatePrefab,transform);
            statue=Instantiate(statuePrefab,transform);
            PositionDoor();
            PlaceOnGround(gate,new Vector3(-236f,0f,60f),2.8f);
            PlaceOnGround(statue,new Vector3(-220f,0f,57f),2f);
        }

        private void PlaceOnGround(GameObject item,Vector3 p,float width)
        {
            item.transform.position=new Vector3(p.x,Pvt2Scenario.GroundHeight(p.x,p.z),p.z);
            Bounds b=RenderBounds(item);
            if (b.size.x>0f) item.transform.localScale*=width/b.size.x;
            b=RenderBounds(item);
            item.transform.position+=Vector3.up*(item.transform.position.y-b.min.y);
        }

        private void PositionDoor()
        {
            if (door==null) return;
            if (!doorHost.IsValid || !world.TryGet(doorHost,out PieceData host))
            { door.SetActive(false);return; }
            WallOpening passage=default;
            foreach (WallOpening opening in host.Openings)
                if (opening.Kind==WallOpeningKind.Passage) { passage=opening;break; }
            if (passage.Id==Guid.Empty) { door.SetActive(false);return; }
            door.SetActive(true);
            door.transform.rotation=host.Transform.Rotation;
            door.transform.localScale=Vector3.one;
            door.transform.position=host.Transform.Position+
                host.Transform.Rotation*new Vector3(
                    passage.Left+passage.Width*0.5f-host.WallDimensions.Length*0.5f,
                    passage.Bottom,host.WallDimensions.Thickness*0.5f+0.08f);
            Bounds b=RenderBounds(door);
            float scale=Mathf.Min(passage.Width/Mathf.Max(0.01f,b.size.x),
                passage.Height/Mathf.Max(0.01f,b.size.y));
            door.transform.localScale=Vector3.one*scale;
            b=RenderBounds(door);
            Vector3 expected=host.Transform.Position+host.Transform.Rotation*
                new Vector3(passage.Left+passage.Width*0.5f-host.WallDimensions.Length*0.5f,
                    passage.Bottom,host.WallDimensions.Thickness*0.5f+0.08f);
            door.transform.position+=new Vector3(expected.x-b.center.x,expected.y-b.min.y,
                expected.z-b.center.z);
        }

        private static Bounds RenderBounds(GameObject item)
        {
            Renderer[] renderers=item.GetComponentsInChildren<Renderer>();
            if (renderers.Length==0) throw new InvalidOperationException("CC0 prop has no renderer.");
            Bounds b=renderers[0].bounds;
            for (int i=1;i<renderers.Length;i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private void OnGUI()
        {
            if (!enabled || hideUi || engine==null) return;
            GUILayout.BeginArea(new Rect(12,12,Mathf.Min(Screen.width-24,930),112),GUI.skin.box);
            GUILayout.BeginHorizontal();
            for (int i=0;i<Views.Length;i++)
            { int index=i;if (GUILayout.Button(Views[i],GUILayout.Height(25)))
                { PositionCamera(index);environment.UpdateStreaming(sceneCamera.transform.position); } }
            if (GUILayout.Button("Capture 5 views",GUILayout.Height(25))) StartCoroutine(CaptureViews());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add block")) AddBlock();
            if (GUILayout.Button("Move +X")) ModifySelected(piece=>piece.WithTransform(
                new PieceTransform(piece.Transform.Position+Vector3.right,piece.Transform.Rotation)));
            if (GUILayout.Button("Resize +X")) ModifySelected(piece=>piece.WithDimensions(
                piece.Dimensions.Resize(0,piece.Dimensions.X+0.25f)));
            if (GUILayout.Button("Change material")) ModifySelected(piece=>piece.WithMaterial(
                piece.MaterialId==LabMaterialIds.Stone ? LabMaterialIds.Brick : LabMaterialIds.Stone));
            if (GUILayout.Button("Delete")) DeleteSelected();
            if (GUILayout.Button("Undo")) { if (history.TryUndo(out _)) Rebuild(); }
            if (GUILayout.Button("Redo")) { if (history.TryRedo(out _)) Rebuild(); }
            if (GUILayout.Button("Save")) Save();
            if (GUILayout.Button("Load")) Load();
            GUILayout.EndHorizontal();
            GUILayout.Label($"Pieces {world.Count} | selected {(hasSelection ? selected.ToString() : "none")} | {message}");
            GUILayout.EndArea();
            Event evt=Event.current;
            if (evt.type==EventType.MouseDown && evt.button==0 && evt.mousePosition.y>126f)
            {
                Ray ray=sceneCamera.ScreenPointToRay(new Vector3(evt.mousePosition.x,
                    Screen.height-evt.mousePosition.y,0f));
                if (engine.TryPick(ray,2500f,out PieceId id)) Select(id);
                else ClearSelection();
                evt.Use();
            }
        }

        private void Select(PieceId id)
        {
            if (hasSelection && selected==id) return;
            ClearSelection();
            selected=id;hasSelection=true;engine.BeginEdit(id);
            message="Selected logical PieceId; edit commands use ConstructionCommandHistory.";
        }

        private void ClearSelection()
        {
            if (hasSelection && engine!=null) engine.EndEdit(selected);
            hasSelection=false;selected=default;
        }

        private void ModifySelected(Func<PieceData,PieceData> transform)
        {
            if (!hasSelection || !world.TryGet(selected,out PieceData prior))
            { message="Select a piece first.";return; }
            try
            { if (history.Update(selected,transform(prior)).Changed) Rebuild(); }
            catch (Exception error) { message=error.Message;Debug.LogWarning(error.Message,this); }
        }

        private void AddBlock()
        {
            Vector3 p=sceneCamera.transform.position+sceneCamera.transform.forward*12f;
            p.y=Pvt2Scenario.GroundHeight(p.x,p.z);
            var piece=new PieceData(PieceId.Parse(Guid.NewGuid().ToString("N")),
                new PieceTransform(p,Quaternion.identity),new BlockDimensions(1.2f,1.8f,1.2f))
                .WithMaterial(LabMaterialIds.Stone);
            if (history.Create(piece).Changed) { Rebuild();Select(piece.Id); }
        }

        private void DeleteSelected()
        {
            if (!hasSelection) { message="Select a piece first.";return; }
            PieceId id=selected;ClearSelection();
            if (history.Delete(id).Changed) Rebuild();
        }

        private void Rebuild()
        { engine.RebuildDirty();message="Logical edit applied and affected pages rebuilt."; }

        private void Save()
        { try { Pvt2Snapshot.Save(world,SavePath);message="Saved: "+SavePath; }
          catch (Exception error) { message=error.Message;Debug.LogException(error,this); } }

        private void Load()
        {
            if (!File.Exists(SavePath)) { message="No saved city at "+SavePath;return; }
            try
            {
                ConstructionWorld replacement=Pvt2Snapshot.Load(SavePath);
                world.Changed-=OnWorldChanged;
                BuildWorld(replacement.Pieces);
                PositionDoor();
                message="Loaded logical city and rebuilt visual pages.";
            }
            catch (Exception error) { message=error.Message;Debug.LogException(error,this); }
        }

        private void PositionCamera(int index)
        {
            viewIndex=index;
            float ground=Pvt2Scenario.GroundHeight(-220f,0f);
            Vector3 position,target;
            switch (index)
            {
                case 0: position=new Vector3(-223f,ground+3.2f,-82f);
                    target=new Vector3(-236f,ground+2.8f,-82f);break;
                case 1: position=new Vector3(-226f,ground+2.1f,-61f);
                    target=new Vector3(-239f,ground+2.4f,-61f);break;
                case 2: position=new Vector3(-220f,ground+4.5f,-124f);
                    target=new Vector3(-220f,ground+3.8f,23f);break;
                case 3: position=new Vector3(-351f,ground+68f,-116f);
                    target=new Vector3(-220f,ground+4f,22f);break;
                default: position=new Vector3(-218f,ground+9f,44f);
                    target=new Vector3(-220f,ground+8f,79f);break;
            }
            sceneCamera.transform.position=position;
            sceneCamera.transform.LookAt(target);
            message="View: "+Views[index]+" ("+(index+1)+"/5)";
        }

        private IEnumerator CaptureViews()
        {
            hideUi=true;
            int previous=viewIndex;
            string stamp=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            for (int i=0;i<Views.Length;i++)
            {
                PositionCamera(i);
                environment.UpdateStreaming(sceneCamera.transform.position);
                yield return null;yield return new WaitForEndOfFrame();
                string path=Path.Combine(Application.persistentDataPath,
                    "aedifica_visual_"+stamp+"_"+Views[i]+".png");
                Texture2D screenshot=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(path,screenshot.EncodeToPNG());
                Destroy(screenshot);
            }
            hideUi=false;
            PositionCamera(previous);
            message="Five captures saved in "+Application.persistentDataPath;
        }

        private void OnDestroy()
        {
            if (world!=null) world.Changed-=OnWorldChanged;
            if (engine!=null) engine.Dispose();
            if (environment!=null) environment.Dispose();
        }
    }
}
