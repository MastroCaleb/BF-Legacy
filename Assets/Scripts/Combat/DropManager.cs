using System.Collections.Generic;
using UnityEngine;

public class DropManager : MonoBehaviour
{
    public static DropManager Instance;

    [Header("Shared Settings")]
    public float gravity = -2000f;
    public float horizontalSpeed = 100f;
    public float bounceDamping = 0.5f;
    public float stopVelocity = 50f;
    public float rotationSpeed = 300f; // degrees/sec

    private readonly List<DropData> drops = new List<DropData>(1024);

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        for (int i = drops.Count - 1; i >= 0; i--)
        {
            DropData d = drops[i];
            if (!d.isBouncing && Mathf.Approximately(d.rotationZ, 0f))
                continue;

            RectTransform rect = d.rect;
            if(d.rect == null) continue;
            Vector3 pos = rect.localPosition;

            // ───── ROTATION (same as original) ─────
            d.rotationZ = Mathf.MoveTowardsAngle(
                d.rotationZ,
                0f,
                rotationSpeed * dt
            );

            // ───── VERTICAL MOTION ─────
            if (d.isBouncing)
            {
                d.velocityY += gravity * dt;
                pos.y += d.velocityY * dt;

                // ───── HORIZONTAL MOTION ─────
                pos.x = Mathf.MoveTowards(pos.x, d.targetX, horizontalSpeed * dt);

                // ───── BOUNCE ─────
                if (pos.y <= d.bounceY && d.velocityY < 0f)
                {
                    pos.y = d.bounceY;
                    d.velocityY = -d.velocityY * bounceDamping;

                    if (Mathf.Abs(d.velocityY) < stopVelocity)
                    {
                        d.velocityY = 0f;
                        d.isBouncing = false;
                    }
                }
            }

            rect.localPosition = pos;
            rect.localEulerAngles = new Vector3(0f, 0f, d.rotationZ);

            drops[i] = d;
        }
    }

    public void Register(DropData drop)
    {
        drops.Add(drop);
    }
}

