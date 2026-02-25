using UnityEngine;
using System.Collections.Generic;

public class RandomWalkerRabbit : MonoBehaviour
{
    public float moveRadius = 30f;
    public float moveSpeed = 3f;
    public float waitTime = 2f;

    private VolterraStatus volterra; 
    private Vector3 targetPosition;
    private float waitCounter = 0f;
    private bool isWaiting = false;
    private bool isBeingHunted = false;
    private GameObject terrainObject;
    private Terrain terrain;
    private Animator animator;

    public List<GameObject> huntingWolves = new List<GameObject>();

    void Start()
    {
        terrainObject = GameObject.Find("MainTerrainDef");
        terrain = terrainObject.GetComponent<Terrain>();
        animator = GetComponentInChildren<Animator>();
        volterra = GameObject.Find("UIManager").GetComponent<VolterraStatus>();
        animator.applyRootMotion = false;
        //Debug.LogError("-------CONEJOOOOOOOOOOOS-------");
        //Debug.Log("-----ANIMATOR ----" + animator);
        if (animator != null)
            animator.SetInteger("AnimIndex", 1);
            animator.SetTrigger("Next");

        SetNewTargetPosition();
    }

    void Update()
    {
        if (isWaiting)
        {
            waitCounter -= Time.deltaTime;

            if (waitCounter <= 0f)
            {
                isWaiting = false;

                if (animator != null)
                    animator.SetInteger("AnimIndex", 1);
                    animator.SetTrigger("Next");

                SetNewTargetPosition();
            }

            return;
        }

        Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        // Reajustar altura según el terreno
        float terrainY = terrain.SampleHeight(nextPos) + terrain.transform.position.y;
        float bottomOffset = GetBottomOffset(gameObject);
        nextPos.y = terrainY + bottomOffset;

        transform.position = nextPos;

        // Solo rotar si se está moviendo realmente
        Vector3 flatDirection = new Vector3(
            targetPosition.x - transform.position.x,
            0f,
            targetPosition.z - transform.position.z
        );

        if (flatDirection.magnitude > 0.1f)
        {
            Quaternion toRotation = Quaternion.LookRotation(flatDirection.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, Time.deltaTime * 5f);
        }

        // Si ya llegó al destino, detener animación y entrar en modo espera
        if (!isWaiting && Vector3.Distance(transform.position, targetPosition) < 0.5f)
        {
            isWaiting = true;
            waitCounter = waitTime;

            if (animator != null) {
                animator.SetInteger("AnimIndex", 0);
                animator.SetTrigger("Next");
            }
        }
    }

    public void AddHunter(GameObject wolf)
    {
        Debug.Log("Esta siendo cazado" + isBeingHunted);
        if (!huntingWolves.Contains(wolf))
        {
            huntingWolves.Add(wolf);
            isBeingHunted = true;
        } 
    }

    public void RemoveHunter(GameObject wolf)
    {
        if (huntingWolves.Contains(wolf))
        {
            huntingWolves.Remove(wolf);
            if (huntingWolves.Count == 0)
            {
                isBeingHunted = false;
            }
        }
    }

    float GetBottomOffset(GameObject obj)
    {
        Renderer renderer = obj.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            float bottomY = renderer.bounds.min.y;
            float pivotY = obj.transform.position.y;
            return -(bottomY - pivotY);
        }
        return 0f;
    }

    void SetNewTargetPosition()
    {
        Vector3 currentPos = transform.position;

        float randomX = Random.Range(-moveRadius, moveRadius);
        float randomZ = Random.Range(-moveRadius, moveRadius);

        float newX = Mathf.Clamp(currentPos.x + randomX, terrain.transform.position.x, terrain.transform.position.x + terrain.terrainData.size.x);
        float newZ = Mathf.Clamp(currentPos.z + randomZ, terrain.transform.position.z, terrain.transform.position.z + terrain.terrainData.size.z);

        float newY = terrain.SampleHeight(new Vector3(newX, 0, newZ)) + terrain.transform.position.y;
        float bottomOffset = GetBottomOffset(gameObject);
        newY += bottomOffset;

        targetPosition = new Vector3(newX, newY, newZ);
    }

    public void beenShot(){
        Debug.LogError("Me han disparado CONEJO");
        volterra.removePrey(-1, gameObject);
    }
}
