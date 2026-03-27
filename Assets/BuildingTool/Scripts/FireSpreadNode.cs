using System.Collections;
using UnityEngine;

namespace BuildingSystem
{
    public class FireSpreadNode : MonoBehaviour
    {
        private ParticleSystem pillarPS;
        private ParticleSystem ceilingPS;

        private float ceilingDist = 10f;
        private bool hasCeiling = false;
        private Vector3 ceilingHitPoint;
        private Vector3 ceilingNormal;

        private bool ceilingFireSpawned = false;

        public void Initialize(FireSpawnpoint _)
        {
            // Calculate ceiling distance immediately
            ceilingHitPoint = transform.position + Vector3.up * 10f;
            ceilingNormal = Vector3.down;

            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.up, out RaycastHit hit, 50f))
            {
                ceilingDist = hit.distance;
                ceilingHitPoint = hit.point;
                ceilingNormal = hit.normal;
                hasCeiling = true;
            }

            // Create Pillar Fire
            GameObject pillarObj = CreateBaseParticleObject("FirePillar", transform);
            pillarObj.transform.rotation = Quaternion.LookRotation(Vector3.up);
            pillarPS = pillarObj.GetComponent<ParticleSystem>();
            pillarPS.Play();

            if (hasCeiling)
            {
                // Create Ceiling Fire but don't play it yet
                GameObject ceilingObj = CreateBaseParticleObject("CeilingJet", transform);
                // We will position it correctly in Update based on particle size to prevent clipping
                ceilingObj.transform.rotation = Quaternion.LookRotation(Vector3.down);
                ceilingPS = ceilingObj.GetComponent<ParticleSystem>();

                // Time it so the ceiling fire starts exactly when the pillar reaches the roof
                float reachTime = ceilingDist / FireTool.Instance.upwardSpeed;
                StartCoroutine(SpawnCeilingFireDelay(reachTime));
            }
        }

        private IEnumerator SpawnCeilingFireDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ceilingPS.Play();
            ceilingFireSpawned = true;
        }

        private void Update()
        {
            if (FireTool.Instance == null) return;

            // Live-update Pillar
            if (pillarPS != null)
            {
                var pMain = pillarPS.main;
                pMain.startSpeed = FireTool.Instance.upwardSpeed;
                pMain.startLifetime = ceilingDist / FireTool.Instance.upwardSpeed;
                pMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, FireTool.Instance.pillarParticleSize);

                var pEmission = pillarPS.emission;
                pEmission.rateOverTime = FireTool.Instance.pillarEmissionMultiplier * ceilingDist;

                var pShape = pillarPS.shape;
                pShape.shapeType = ParticleSystemShapeType.Cone;
                pShape.angle = 1f;
                pShape.radius = FireTool.Instance.pillarRadius;
            }

            // Live-update Ceiling Fire
            if (ceilingFireSpawned && ceilingPS != null)
            {
                var cMain = ceilingPS.main;
                cMain.startSpeed = FireTool.Instance.ceilingSpreadSpeed;
                cMain.startLifetime = FireTool.Instance.ceilingMaxRadius / FireTool.Instance.ceilingSpreadSpeed;
                cMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, FireTool.Instance.ceilingParticleSize);
                cMain.gravityModifier = FireTool.Instance.ceilingGravity;
                
                // Prevent particle textures from visually piercing through paper-thin ceilings
                float lowestOffset = (FireTool.Instance.ceilingParticleSize * 0.5f) + 0.05f;
                ceilingPS.transform.position = ceilingHitPoint + Vector3.down * lowestOffset;

                var cEmission = ceilingPS.emission;
                cEmission.rateOverTime = FireTool.Instance.ceilingEmissionRate;

                // Adjust the cone angle based on user preference (89.9 is perfectly flat)
                var cShape = ceilingPS.shape;
                cShape.shapeType = ParticleSystemShapeType.Cone;
                cShape.angle = 89.5f; 
                cShape.radius = 0.2f;

                var cColl = ceilingPS.collision;
                if (!cColl.enabled)
                {
                    cColl.enabled = true;
                    cColl.type = ParticleSystemCollisionType.World;
                    cColl.mode = ParticleSystemCollisionMode.Collision3D;
                    cColl.quality = ParticleSystemCollisionQuality.High; // Forces exact physics raycasts to prevent clipping through ceilings
                    cColl.bounce = 0.1f;
                    cColl.dampen = 0.8f;
                    cColl.radiusScale = 0.6f; // Make the particle physics sphere slightly larger than the visual to stop them earlier
                    cColl.collidesWith = Physics.AllLayers;
                }
            }
        }

        private GameObject CreateBaseParticleObject(string objName, Transform parent)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startColor = new Color(1f, 0.35f, 0f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = true;

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(1f, 0.9f, 0.1f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.3f, 0.0f), 0.3f),
                    new GradientColorKey(new Color(0.15f, 0.15f, 0.15f), 0.7f),
                    new GradientColorKey(Color.black, 1.0f)
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f), 
                    new GradientAlphaKey(0.9f, 0.4f), 
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            colorOverLife.color = grad;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.2f);
            curve.AddKey(0.3f, 1f);
            curve.AddKey(1f, 0.1f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            return go;
        }
    }
}
