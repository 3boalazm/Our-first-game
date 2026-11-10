using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class birdScript : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public Sprite flappingSprite;
    public Sprite originalSprite;
    public Sprite deadBirdSprite;
    public Rigidbody2D myRigidbody;
    public float flapStrength;
    public float baseGravityScale;
    public logicScript logic;
    public bool birdIsAlive = true;

    // Start is called before the first frame update
    void Start()
    {
        logic = GameObject.FindGameObjectWithTag("Logic").GetComponent<logicScript>();
    }

    // Update is called once per frame
    void Update()
    {
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) && birdIsAlive == true)
        {
            spriteRenderer.sprite = flappingSprite;
            myRigidbody.velocity = Vector2.up * flapStrength;
            StartCoroutine(ResetSpriteAfterDelay());
        }
    }

    IEnumerator ResetSpriteAfterDelay()
    {
        yield return new WaitForSeconds(0.15f); // Adjust delay as needed
        spriteRenderer.sprite = originalSprite; // Assign the idle sprite in the Inspector
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        logic.gameOver();
        birdIsAlive = false;
        spriteRenderer.sprite = deadBirdSprite;
        GetComponent<Rigidbody2D>().gravityScale *= 0.4f;
    }
}
