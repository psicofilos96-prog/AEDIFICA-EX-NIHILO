using System;
using System.Collections;
using System.IO;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // One residence; all architectural edits go through ConstructionCommandHistory.
    public sealed class PvtFreeAssetsReferenceRunner : MonoBehaviour
    {
        private static readonly string[] Views={"finished facade","door/window/eave detail","street","landscape"};
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Material stone,brick,wood,plaster,roof,road,terrain,glass;
        [SerializeField] private Texture2D dryAlbedo,dryNormal,dryMask,groundAlbedo,groundNormal,groundMask;
        [SerializeField] private GameObject grass,rock,pot,shrub;
        private ConstructionWorld world;
        private ConstructionCommandHistory history;
        private PvtChunkVisualEngine engine;
        private PvtFreeReferenceEnvironment environment;
        private GameObject visualRoot;
        private PieceId selected;
        private bool hasSelection,hideUi;
        private int viewIndex;
        private string message="Click an architectural piece to select it.";
        private string SavePath=>Path.Combine(Application.persistentDataPath,"aedifica_free_reference.pvt2");
        public ConstructionWorld World=>world;
        public PvtChunkVisualEngine VisualEngine=>engine;

        public void Configure(Camera camera,Material stoneMaterial,Material brickMaterial,
            Material woodMaterial,Material plasterMaterial,Material roofMaterial,Material roadMaterial,
            Material terrainMaterial,Material glassMaterial,Texture2D dryBase,Texture2D dryN,
            Texture2D dryM,Texture2D groundBase,Texture2D groundN,Texture2D groundM,
            GameObject grassPrefab,GameObject rockPrefab,GameObject potPrefab,GameObject shrubPrefab)
        {
            sceneCamera=camera;stone=stoneMaterial;brick=brickMaterial;wood=woodMaterial;
            plaster=plasterMaterial;roof=roofMaterial;road=roadMaterial;terrain=terrainMaterial;
            glass=glassMaterial;dryAlbedo=dryBase;dryNormal=dryN;dryMask=dryM;
            groundAlbedo=groundBase;groundNormal=groundN;groundMask=groundM;
            grass=grassPrefab;rock=rockPrefab;pot=potPrefab;shrub=shrubPrefab;
        }

        private void Start()
        {
            if (sceneCamera==null || stone==null || brick==null || wood==null || plaster==null ||
                roof==null || road==null || terrain==null || glass==null || dryAlbedo==null ||
                dryNormal==null || dryMask==null || groundAlbedo==null || groundNormal==null ||
                groundMask==null || grass==null || rock==null || pot==null || shrub==null)
            { Debug.LogError("Free asset reference scene has missing serialized dependencies.",this);enabled=false;return; }
            var registry=new MaterialRegistry(stone,stone,brick,plaster);
            registry.Register(PvtFinalScenario.Wood,wood);
            registry.Register(PvtFinalScenario.Ceramic,roof);
            registry.Register(PvtVisualCityScenario.Glass,glass);
            environment=new PvtFreeReferenceEnvironment(transform,terrain,road,stone,
                dryAlbedo,dryNormal,dryMask,groundAlbedo,groundNormal,groundMask,
                grass,rock,pot,shrub);
            BuildWorld(PvtVisualCityScenario.BuildReferenceResidence(),registry);
            PositionCamera(0);
            Debug.Log($"Free asset reference ready: {world.Count} individually editable pieces; art awaits human review.",this);
        }

        private void BuildWorld(System.Collections.Generic.IEnumerable<PieceData> pieces,MaterialRegistry registry)
        {
            ClearSelection();
            if (engine!=null) engine.Dispose();
            if (visualRoot!=null) Destroy(visualRoot);
            world=new ConstructionWorld();
            foreach (PieceData piece in pieces)
                if (!world.Create(piece).Changed) throw new InvalidOperationException("Duplicate reference PieceId.");
            history=new ConstructionCommandHistory(world,512);
            visualRoot=new GameObject("Editable residence PieceData");
            visualRoot.transform.SetParent(transform,false);
            engine=new PvtChunkVisualEngine(world,registry,visualRoot.transform);
            engine.RebuildDirty();
        }

        private void OnGUI()
        {
            if (!enabled || hideUi || engine==null) return;
            GUILayout.BeginArea(new Rect(12,12,Mathf.Min(Screen.width-24,1060),151),GUI.skin.box);
            GUILayout.BeginHorizontal();
            for (int i=0;i<Views.Length;i++)
            { int index=i;if (GUILayout.Button(Views[i])) PositionCamera(index); }
            if (GUILayout.Button("Capture 4 views")) StartCoroutine(CaptureViews());
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add door frame")) AddFinish(ArchitecturalFinishKind.DoorFrame);
            if (GUILayout.Button("Add window frame")) AddFinish(ArchitecturalFinishKind.WindowFrameSill);
            if (GUILayout.Button("Add lintel")) AddFinish(ArchitecturalFinishKind.Lintel);
            if (GUILayout.Button("Add roof eave")) AddFinish(ArchitecturalFinishKind.RoofEave);
            if (GUILayout.Button("Remove last finish")) ModifySelected(piece=>piece.Finishes.Count==0
                ? throw new InvalidOperationException("Selected piece has no finish.")
                : piece.WithoutFinish(piece.Finishes[piece.Finishes.Count-1].Id));
            if (GUILayout.Button("Opening +0.1m")) ModifySelected(piece=>piece.Openings.Count==0
                ? throw new InvalidOperationException("Selected wall has no opening.")
                : piece.ReplaceOpening(piece.Openings[0].WithRect(piece.Openings[0].Left,
                    piece.Openings[0].Bottom,piece.Openings[0].Width+0.1f,piece.Openings[0].Height)));
            GUILayout.EndHorizontal();
            GUILayout.Label($"Pieces {world.Count} | selected {(hasSelection ? selected.ToString() : "none")} | {message}");
            GUILayout.EndArea();
            Event evt=Event.current;
            if (evt.type==EventType.MouseDown && evt.button==0 && evt.mousePosition.y>166f)
            {
                Ray ray=sceneCamera.ScreenPointToRay(new Vector3(evt.mousePosition.x,
                    Screen.height-evt.mousePosition.y,0f));
                if (engine.TryPick(ray,250f,out PieceId id)) Select(id);
                else ClearSelection();
                evt.Use();
            }
        }

        private void Select(PieceId id)
        { ClearSelection();selected=id;hasSelection=true;engine.BeginEdit(id);message="Selected logical PieceId."; }
        private void ClearSelection()
        { if (hasSelection && engine!=null) engine.EndEdit(selected);hasSelection=false;selected=default; }
        private void ModifySelected(Func<PieceData,PieceData> change)
        {
            if (!hasSelection || !world.TryGet(selected,out PieceData prior))
            { message="Select an architectural piece first.";return; }
            try { if (history.Update(selected,change(prior)).Changed) Rebuild(); }
            catch (Exception error) { message=error.Message;Debug.LogWarning(error.Message,this); }
        }
        private void AddFinish(ArchitecturalFinishKind kind)
        {
            ModifySelected(piece=>
            {
                Guid openingId=Guid.Empty;
                if (kind==ArchitecturalFinishKind.RoofEave)
                {
                    if (!piece.Dimensions.IsRoof)
                        throw new InvalidOperationException("Select a roof for a linked eave.");
                }
                else
                {
                    if (piece.Type!=PieceType.Wall)
                        throw new InvalidOperationException("Select a wall for an opening finish.");
                    foreach (WallOpening opening in piece.Openings)
                    {
                        if (kind==ArchitecturalFinishKind.DoorFrame && opening.Kind!=WallOpeningKind.Passage ||
                            kind==ArchitecturalFinishKind.WindowFrameSill && opening.Kind!=WallOpeningKind.Window)
                            continue;
                        bool already=false;
                        foreach (ArchitecturalFinish existing in piece.Finishes)
                            if (existing.OpeningId==opening.Id && existing.Kind==kind) { already=true;break; }
                        if (!already) { openingId=opening.Id;break; }
                    }
                    if (openingId==Guid.Empty)
                        throw new InvalidOperationException("No compatible unframed opening remains.");
                }
                MaterialId material=kind==ArchitecturalFinishKind.RoofEave
                    ? PvtFinalScenario.Wood : LabMaterialIds.Stone;
                return piece.WithFinish(new ArchitecturalFinish(Guid.NewGuid(),piece.Id,
                    openingId,kind,material));
            });
        }

        private void AddBlock()
        {
            Vector3 p=sceneCamera.transform.position+sceneCamera.transform.forward*8f;
            p.y=Pvt2Scenario.GroundHeight(p.x,p.z);
            var piece=new PieceData(PieceId.Parse(Guid.NewGuid().ToString("N")),
                new PieceTransform(p,Quaternion.identity),new BlockDimensions(1f,1f,1f))
                .WithMaterial(LabMaterialIds.Stone);
            if (history.Create(piece).Changed) { Rebuild();Select(piece.Id); }
        }
        private void DeleteSelected()
        { if (!hasSelection) { message="Select a piece first.";return; }
          PieceId id=selected;ClearSelection();if (history.Delete(id).Changed) Rebuild(); }
        private void Rebuild()
        { engine.RebuildDirty();message="Logical edit applied; affected visual pages rebuilt."; }
        private void Save()
        { try { Pvt2Snapshot.Save(world,SavePath);message="Saved: "+SavePath; }
          catch (Exception error) { message=error.Message;Debug.LogException(error,this); } }
        private void Load()
        {
            if (!File.Exists(SavePath)) { message="No saved reference at "+SavePath;return; }
            try
            {
                ConstructionWorld loaded=Pvt2Snapshot.Load(SavePath);
                MaterialRegistry registry=new MaterialRegistry(stone,stone,brick,plaster);
                registry.Register(PvtFinalScenario.Wood,wood);
                registry.Register(PvtFinalScenario.Ceramic,roof);
                registry.Register(PvtVisualCityScenario.Glass,glass);
                BuildWorld(loaded.Pieces,registry);
                message="Loaded logical pieces and rebuilt visual pages.";
            }
            catch (Exception error) { message=error.Message;Debug.LogException(error,this); }
        }
        private void PositionCamera(int index)
        {
            viewIndex=index;
            float ground=Pvt2Scenario.GroundHeight(0f,0f);
            Vector3 position,target;
            switch (index)
            {
                case 0: position=new Vector3(2f,ground+2.8f,-14f);
                    target=new Vector3(0f,ground+2.4f,-2f);break;
                case 1: position=new Vector3(4f,ground+2.2f,-7.2f);
                    target=new Vector3(2f,ground+2f,-3.5f);break;
                case 2: position=new Vector3(-17f,ground+4.8f,-20f);
                    target=new Vector3(0f,ground+3f,-1f);break;
                default: position=new Vector3(-28f,ground+21f,-32f);
                    target=new Vector3(0f,ground+2f,0f);break;
            }
            sceneCamera.transform.position=position;sceneCamera.transform.LookAt(target);
            message="View: "+Views[index]+" ("+(index+1)+"/4)";
        }
        private IEnumerator CaptureViews()
        {
            hideUi=true;int previous=viewIndex;
            string stamp=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            for (int i=0;i<Views.Length;i++)
            {
                PositionCamera(i);yield return null;yield return new WaitForEndOfFrame();
                string path=Path.Combine(Application.persistentDataPath,
                    "aedifica_free_reference_"+stamp+"_"+Views[i]+".png");
                Texture2D screenshot=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(path,screenshot.EncodeToPNG());Destroy(screenshot);
            }
            hideUi=false;PositionCamera(previous);
            message="Four captures saved in "+Application.persistentDataPath;
        }
        private void OnDestroy()
        { if (engine!=null) engine.Dispose();if (environment!=null) environment.Dispose(); }
    }
}
