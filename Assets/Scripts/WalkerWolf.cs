using UnityEngine;
using Unity.Netcode;

public class WalkerWolf : NetworkBehaviour
{
    //public float moveRadius = 30f;
    public float moveSpeed = 3f;
    public float waitTime = 2f;

    private VolterraStatus volterra;
    private GameObject availableRabbits;
    private GameObject followingRabbit = null; //Rabbit that is being followed by the wolf
    private Vector3 targetPosition;
    //private float waitCounter = 0f; 
    private bool isWaiting = false;
    private GameObject terrainObject;
    private Terrain terrain;
    private Animator animator;
    private float chaseTime = 5; //Hunting time until idle
    private float currentTime = 0; //Time the wolf has been actively hunting
    private bool isAttacking = false;

    void Start()
    {
        terrainObject = GameObject.Find("MainTerrainDef");
        terrain = terrainObject.GetComponent<Terrain>();
        availableRabbits = GameObject.Find("PreyParent");
        animator = GetComponentInChildren<Animator>();
        animator.applyRootMotion = false;
        volterra = GameObject.Find("UIManager").GetComponent<VolterraStatus>();

        if (animator != null)
            animator.SetBool("isWalking", true);

        SetNewTargetPosition();
    }

    public void setAttack(bool attack)
    {
        isAttacking = attack;

        if (isAttacking)
        {
            // Dispara el trigger para que la animación se ejecute
            animator.SetTrigger("TriggerAttack");
            isAttacking = false; // Opcional, dependiendo de la lógica, se resetea la variable
        }
    }

    public GameObject getFollowingRabbit() {
        return this.followingRabbit;
    }

    GameObject getCloserRabbit(){
        //Current wolf position
        Vector3 wolfPos = transform.position;
        GameObject closerRabbit = null;
        float minDistance = Mathf.Infinity;
        if (availableRabbits != null)
        {
            foreach (Transform child in availableRabbits.transform)
            {
             float distance = Vector3.Distance(wolfPos, child.position); 
             if (distance < minDistance) {
                minDistance = distance;
                closerRabbit = child.gameObject;
             }  
            }

            if (closerRabbit != null)
            {
                Debug.Log("El conejo más cercano está en: " + closerRabbit.transform.position);
                return closerRabbit;
            }
            return null;
        }
        return null;
        

    }

    void SetNewTargetPosition()
    {
        Vector3 currentPos = transform.position;
        Vector3 rabbitPos;
        Debug.Log("---------- BUSCANDO CONEJO -----------");
        Debug.Log(availableRabbits);
        //if the wolf is not following a rabbit
        if (followingRabbit == null) {
            Debug.Log("---- ENTRA A BUSCAR CONEJO ----");
            followingRabbit = getCloserRabbit();
            if (followingRabbit != null) {
                RandomWalkerRabbit rabbitScript = followingRabbit.GetComponent<RandomWalkerRabbit>();
                if (rabbitScript != null)
                {
                    rabbitScript.AddHunter(this.gameObject);
                }
            }
        }

        if (followingRabbit != null) 
        {
            rabbitPos = followingRabbit.transform.position;
            Debug.Log("---------- CONEJO ENCONTRADO ---------");

            float newY = terrain.SampleHeight(currentPos) + terrain.transform.position.y;
            float bottomOffset = GetBottomOffset(gameObject);
            newY += bottomOffset;

            // Se mueve hacia el conejo
            targetPosition = new Vector3(rabbitPos.x, newY, rabbitPos.z);
            Debug.Log("---- PERSIGUIENDO CONEJO ---- " + targetPosition);
        } 
        else 
        {
            // 2. Si es null, el objetivo es su propia posición (se queda quieto)
            targetPosition = transform.position; 
            Debug.Log("---- SIN OBJETIVO: EL DEPREDADOR SE QUEDA QUIETO ----");
        }

    }

    void Update()
    {
        SetNewTargetPosition();
        /*if (isWaiting)
        {
            waitCounter -= Time.deltaTime;

            if (waitCounter <= 0f)
            {
                isWaiting = false;

                if (animator != null)
                    animator.SetBool("isWalking", true);

                SetNewTargetPosition();
            }

            return;
        }*/

        currentTime += Time.deltaTime;

        if (isWaiting)
        {
            // If idle, check how much time has passed and start chasing again
            if (currentTime >= waitTime)
            {
                //Debug.LogError("---- EMPIEZA A MOVERSE EL LOBO ----");
                isWaiting = false;
                animator.SetBool("isWalking", true);
                currentTime = 0;
            }
        } else {
            Debug.Log("--- LLEGA A MoveTowards CONEJO ----");
            Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            Debug.Log("--- PASA MoveTowards CONEJO ----");
            float terrainY = terrain.SampleHeight(nextPos) + terrain.transform.position.y;
            float bottomOffset = GetBottomOffset(gameObject);
            nextPos.y = terrainY + bottomOffset;

            transform.position = nextPos;
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
            
            if (currentTime >= chaseTime)
            {
                //Debug.LogError("--- DESTINO CONEJO ---" + Vector3.Distance(transform.position, targetPosition));
                isWaiting = true;
                currentTime = 0f;
                animator.SetBool("isWalking", false);
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

    public void beenShot()
    {
        if (!IsSpawned) return;
        
        BeenShotServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void BeenShotServerRpc()
    {
        Debug.LogError("Me han disparado LOBO");
        volterra.removePredator(-1, gameObject);
    }



    
}
