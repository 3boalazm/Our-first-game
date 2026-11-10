using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pipeMoveScript : MonoBehaviour
{
    public float moveSpeed = 15;
    public float deadZone;
    public logicScript logic; // Reference to logicScript
    public pipeSpawnScript spawner; // Reference to pipeSpawnScript
    void Start()
    {
        logic = GameObject.FindGameObjectWithTag("Logic").GetComponent<logicScript>();
        spawner = GameObject.FindGameObjectWithTag("Spawner").GetComponent<pipeSpawnScript>();
    }

    void Update()
    {
        transform.position = transform.position + (Vector3.left * moveSpeed) * Time.deltaTime;

        if (transform.position.x < deadZone)
        {
            Destroy(gameObject);
        }

        increaseSpeed(); // Call the increaseSpeed method here
    }

    void increaseSpeed()
    {
        if (logic.playerScore >= 25) {
            moveSpeed = 40;
            spawner.spawnRate = 1f;
        }
        else if (logic.playerScore >= 15) {
            moveSpeed = 30;
            spawner.spawnRate = 1.3f;
        } else if (logic.playerScore >= 10) {
            moveSpeed = 25;
            spawner.spawnRate = 1.5f;
        } else if (logic.playerScore >= 5) {
            moveSpeed = 20;
            spawner.spawnRate = 1.7f;
        } else {
            moveSpeed += 0;
            spawner.spawnRate += 0;
        }
    }
}