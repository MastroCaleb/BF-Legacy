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

    [Header("Stability")]
    [Tooltip("Physics is simulated in chunks no bigger than this, so a big frame " +
             "(fast-forward, hitch, or resuming after a freeze) can't tunnel past " +
             "the floor or feed an oversized velocity into the bounce.")]
    public float maxSubStep = 1f / 60f;

    private readonly List<DropData> drops = new List<DropData>(1024);

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // actually paused — nothing to simulate

        for (int i = drops.Count - 1; i >= 0; i--)
        {
            DropData d = drops[i];
            if (!d.isBouncing && Mathf.Approximately(d.rotationZ, 0f))
                continue;

            RectTransform rect = d.rect;
            if (rect == null) continue;
            Vector3 pos = rect.localPosition;

            float remaining = dt;
            while (remaining > 0f)
            {
                float step = Mathf.Min(remaining, maxSubStep);
                remaining -= step;

                // ───── ROTATION ─────
                d.rotationZ = Mathf.MoveTowardsAngle(d.rotationZ, 0f, rotationSpeed * step);

                if (!d.isBouncing)
                    continue; // just settling rotation, nothing else to do this step

                // ───── VERTICAL MOTION ─────
                d.velocityY += gravity * step;
                pos.y += d.velocityY * step;

                // ───── HORIZONTAL MOTION ─────
                pos.x = Mathf.MoveTowards(pos.x, d.targetX, horizontalSpeed * step);

                // ───── BOUNCE ─────
                if (pos.y <= d.bounceY && d.velocityY < 0f)
                {
                    pos.y = d.bounceY;
                    d.velocityY = -d.velocityY * bounceDamping;

                    if (Mathf.Abs(d.velocityY) < stopVelocity)
                    {
                        d.velocityY = 0f;
                        d.isBouncing = false;
                        break; // settled — stop burning the rest of this frame's time
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
