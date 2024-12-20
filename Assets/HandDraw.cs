using System.Security.Cryptography;
using UnityEngine;

public class HandDraw : MonoBehaviour
{
    public Material paintingCanvasMaterial;
    public RenderTexture paintingCanvasRenderTexture;
    public Transform rightControllerTransform; // Assign the controller transform here
    public float dotSize = 5f;
    public Color dotColor = Color.black;
    public float maxDrawingDistance = 0.5f; // Maximum distance to allow drawing
    public Material drawMaterial;
     
   
    public BellSoundGenerator bellSoundGenerator;
    public GameObject audioSourcePrefabInHierarchy;



    private Material canvasMaterial;
    private Texture2D brushTexture;
    private float nextBellTime = 0f; // When to play the next bell
    private float interval = 0.25f;  // Minimum interval between bell sounds



    // List of basic colors to cycle through
    private readonly Color[] colors = new Color[]
    {
    Color.red,
    Color.blue,
    Color.green,
    Color.yellow,
    Color.magenta,
    Color.cyan,
    Color.black,
    Color.white
    };

    // Current color index
    private int currentColorIndex = 0;
       

    private void Start()
    {
        SetupMeshCollider();
        CreateBrushTexture();
    

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            canvasMaterial = renderer.material;

            if (paintingCanvasRenderTexture == null)
            {
                paintingCanvasRenderTexture = new RenderTexture(1024, 1024, 0);
                paintingCanvasRenderTexture.Create();
            }

            ClearCanvas();
            canvasMaterial.mainTexture = paintingCanvasRenderTexture;
        }

        // Find the BellSoundGenerator script dynamically
        bellSoundGenerator = FindFirstObjectByType<BellSoundGenerator>();

        if (bellSoundGenerator == null)
        {
            Debug.LogError("BellSoundGenerator not found in the scene.");
            return;
        }

        // Assign the AudioSourcePrefab from the Hierarchy
        AudioSource audioSourceComponent = audioSourcePrefabInHierarchy.GetComponent<AudioSource>();
        if (audioSourceComponent == null)
        {
            Debug.LogError("AudioSourcePrefabInHierarchy does not have an AudioSource component!");
            return;
        }

        // Initialize the BellSoundGenerator via Setup
        bellSoundGenerator.Setup(audioSourceComponent);
    }

    private void SetupMeshCollider()
    {
        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<MeshCollider>();
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            collider.sharedMesh = meshFilter.sharedMesh;
        }
    }

    private void CreateBrushTexture()
    {
        int brushSize = 32;
        brushTexture = new Texture2D(brushSize, brushSize, TextureFormat.RGBA32, false);
        float center = brushSize / 2f;

        for (int y = 0; y < brushSize; y++)
        {
            for (int x = 0; x < brushSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Max(0, 1 - (distance / (brushSize / 2f)));
                brushTexture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        brushTexture.Apply();
    }

    private void ClearCanvas()
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = paintingCanvasRenderTexture;
        GL.Clear(true, true, Color.white);
        RenderTexture.active = prev;
    }

    // Store the previous UV coordinate globally
    private Vector2? previousUV = null;

    private void Update()
    {
        if (paintingCanvasRenderTexture == null || rightControllerTransform == null || canvasMaterial == null)
        {
            return;
        }

        // Check if the Y button is pressed
        if (OVRInput.GetDown(OVRInput.Button.Four)) // Y button on left controller
        {
            // Cycle to the next color
            currentColorIndex = (currentColorIndex + 1) % colors.Length;

            // Update the draw material color
            drawMaterial.color = colors[currentColorIndex];
            Debug.Log($"Color switched to: {drawMaterial.color}");
        }

        // Check if the B button is pressed
        if (OVRInput.GetDown(OVRInput.Button.Two)) // B button
        {
            ClearCanvas();
        }

        // Get the grip depth for the right controller
        float gripDepth = OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger);

        // Map the grip depth to the dot size range (3.0 to 100.0)
        dotSize = Mathf.Lerp(3.0f, 100.0f, gripDepth);

        Debug.Log($"Grip Depth: {gripDepth}, Dot Size: {dotSize}");

        // Check if the A button is pressed
        if (OVRInput.Get(OVRInput.Button.One)) // A button on Oculus controller
        {
            // Raycast from the controller
            Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) // No max distance
            {
                Debug.Log($"Raycast Hit: {hit.collider.gameObject.name} on Device.");
                Debug.DrawRay(ray.origin, ray.direction * maxDrawingDistance, Color.green);

                //bellSoundGenerator.StartBellSound();
                PlaySoundCheck();

                if (hit.collider.gameObject.CompareTag("Obstacle")) // Ensure canvas has "Obstacle" tag
                {
                    if (hit.textureCoord != Vector2.zero)
                    {
                        Debug.Log($"Valid UV: {hit.textureCoord}");

                        // Interpolate if we have a previous UV coordinate
                        if (previousUV.HasValue)
                        {
                            InterpolateDots(previousUV.Value, hit.textureCoord);
                        }

                        // Update the previous UV
                        previousUV = hit.textureCoord;
                    }
                }
            }
        }        
        else
        {
            // Reset the previous UV when A button is not pressed
            previousUV = null;
            bellSoundGenerator.StopBellSound();// Random.Range(interval * 0.4f, interval * 1f));
        }
        
    }

    private void PlaySoundCheck()
    {
        // Play a new random bell sound at regular intervals
        if (Time.time >= nextBellTime)
        {
            // Randomly select a note from the A minor scale
            int randomScaleIndex = Random.Range(0, bellSoundGenerator.minorScaleFrequencies.Length);
            bellSoundGenerator.PlayBellSound(randomScaleIndex);

            // Set the next bell time with some randomness for natural variation
            nextBellTime = Time.time + Random.Range(interval * 0.4f, interval * 1f);
        }
    }

    // Function to interpolate and draw dots between two UV points
    private void InterpolateDots(Vector2 startUV, Vector2 endUV)
    {
        float distance = Vector2.Distance(startUV, endUV);
        int steps = Mathf.CeilToInt(distance * 300); // Higher multiplier for smoother results at higher speeds
        steps = Mathf.Max(steps, 10); // Ensure a minimum number of steps for very short distances

        for (int i = 0; i <= steps; i++)
        {
            Vector2 interpolatedUV = Vector2.Lerp(startUV, endUV, (float)i / steps);
            DrawDot(interpolatedUV);
        }
    }


    private void DrawDot(Vector2 uv)
    {
        Vector2 pos = new Vector2(
            uv.x * paintingCanvasRenderTexture.width - (dotSize / 2),
            (1.0f - uv.y) * paintingCanvasRenderTexture.height - (dotSize / 2)
        );

        if (pos.x < 0 || pos.x >= paintingCanvasRenderTexture.width || pos.y < 0 || pos.y >= paintingCanvasRenderTexture.height)
        {
            Debug.LogWarning($"Position out of bounds: {pos}");
            return;
        }

        Graphics.SetRenderTarget(paintingCanvasRenderTexture);

        if (!paintingCanvasRenderTexture.IsCreated())
        {
            Debug.LogError("RenderTexture is not created!");
            return;
        }

        if (drawMaterial == null)
        {
            Debug.LogError("Draw Material is not assigned!");
            return;
        }

        if (!drawMaterial.SetPass(0))
        {
            Debug.LogError("Material.SetPass failed!");
            return;
        }

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, paintingCanvasRenderTexture.width, paintingCanvasRenderTexture.height, 0);

        GL.Begin(GL.QUADS);
        GL.Color(dotColor);
        GL.Vertex3(pos.x, pos.y, 0);
        GL.Vertex3(pos.x + dotSize, pos.y, 0);
        GL.Vertex3(pos.x + dotSize, pos.y + dotSize, 0);
        GL.Vertex3(pos.x, pos.y + dotSize, 0);
        GL.End();

        GL.PopMatrix();
        RenderTexture.active = null;

        Debug.Log("Dot drawn successfully.");
    }

    private void ValidateRenderTextureContent(Vector2 pos)
    {
        RenderTexture.active = paintingCanvasRenderTexture;

        // Create a temporary Texture2D to read pixel data
        Texture2D debugTexture = new Texture2D(
            paintingCanvasRenderTexture.width,
            paintingCanvasRenderTexture.height,
            TextureFormat.RGB24,
            false
        );
        debugTexture.ReadPixels(new Rect(0, 0, paintingCanvasRenderTexture.width, paintingCanvasRenderTexture.height), 0, 0);
        debugTexture.Apply();

        // Read the color of the pixel at the calculated position
        Color pixelColor = debugTexture.GetPixel((int)pos.x, (int)pos.y);
        Debug.Log($"Pixel color at {pos}: {pixelColor}");

        RenderTexture.active = null;
        Destroy(debugTexture);
    }


    private void OnDestroy()
    {
        if (brushTexture != null)
        {
            Destroy(brushTexture);
        }
    }
}
