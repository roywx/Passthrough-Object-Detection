using UnityEngine;
using System.Collections;

public class ARSimulator : MonoBehaviour
{
    // These fields determine the geometry of the virtual screen (in world space)
    public float diagonalFOV = 46f;
    public int width = 40;    // e.g., 40 units for geometry
    public int height = 40;   // e.g., 40 units for geometry
    public float distance = 1f;
    public float verticalOffsetDegrees = 0f;
    public float horizontalOffsetDegrees = 0f;

    // These fields determine the RenderTexture resolution
    public int textureWidth = 1024;   // high resolution for clarity
    public int textureHeight = 1024;

    public RenderTexture renderTexture;
    public GameObject virtualScreen;

    void Start()
    {
        // Set the RenderTexture resolution using textureWidth and textureHeight.
        renderTexture.width = textureWidth;
        renderTexture.height = textureHeight;

        // Calculate aspect ratio for geometry using width and height.
        float aspectRatio = (float)width / (float)height;

        // Calculate horizontal and vertical FOV based on diagonalFOV and aspect ratio.
        (float virtualHorizFOV, float virtualVertFOV) = CalculateFOV(diagonalFOV, aspectRatio);
        Debug.Log("Horizontal FOV: " + virtualHorizFOV);
        Debug.Log("Vertical FOV: " + virtualVertFOV);

        // Set the camera parameters.
        Camera camera = GetComponent<Camera>();
        camera.fieldOfView = virtualVertFOV;
        camera.aspect = aspectRatio;
        Debug.Log("Virtual Camera FOV: " + camera.fieldOfView);

        // Calculate screen (quad) size and position using the FOV and offset parameters.
        (float screenWidth, float screenHeight, float screenX, float screenY) =
            CalculateScreen(virtualHorizFOV, virtualVertFOV, distance, verticalOffsetDegrees, horizontalOffsetDegrees, diagonalFOV);
        Debug.Log("Width: " + screenWidth);
        Debug.Log("Height: " + screenHeight);
        Debug.Log("x position: " + screenX);
        Debug.Log("y position: " + screenY);

        // Set the virtual screen's transform.
        virtualScreen.transform.localPosition = new Vector3(screenX, screenY, distance);
        virtualScreen.transform.localScale = new Vector3(screenWidth, screenHeight, 1);
    }

    public static (float horizontalFOV, float verticalFOV) CalculateFOV(float diagonalFOV, float aspectRatio)
    {
        float aspectWidth = aspectRatio;
        float aspectHeight = 1f;
        float aspectDiag = Mathf.Sqrt(aspectWidth * aspectWidth + aspectHeight * aspectHeight);
        float fovDiagRad = diagonalFOV * Mathf.Deg2Rad;

        // Compute the horizontal FOV
        float fovHorizRad = 2f * Mathf.Atan(Mathf.Tan(fovDiagRad / 2f) * aspectWidth / aspectDiag);
        float horizontalFOV = fovHorizRad * Mathf.Rad2Deg;

        // Compute the vertical FOV
        float fovVertRad = 2f * Mathf.Atan(Mathf.Tan(fovDiagRad / 2f) * (aspectHeight / aspectDiag));
        float verticalFOV = fovVertRad * Mathf.Rad2Deg;

        return (horizontalFOV, verticalFOV);
    }

    public static (float screenWidth, float screenHeight, float screenX, float screenY) CalculateScreen(
        float virtualHorizFOV, float virtualVertFOV, float distance, float vertOffset, float horizOffset, float dFOV)
    {
        float fovHorizRad = virtualHorizFOV * Mathf.Deg2Rad;
        float fovVertRad = virtualVertFOV * Mathf.Deg2Rad;
        float vertOffsetRad = vertOffset * Mathf.Deg2Rad;
        float horizOffsetRad = horizOffset * Mathf.Deg2Rad;

        // Calculate x (horizontal) and y (vertical) offset in world space.
        float xPosition = Mathf.Tan(horizOffsetRad) * distance;
        float yPosition = Mathf.Tan(vertOffsetRad) * distance;

        // Calculate the original width and height of the screen at the specified distance.
        float originalWidth = 2f * Mathf.Tan(fovHorizRad / 2f) * distance;
        float originalHeight = 2f * Mathf.Tan(fovVertRad / 2f) * distance;

        // Calculate a scale factor based on the desired diagonal FOV.
        float ratio = CalculateScaleFactor(xPosition, yPosition, distance, originalWidth, originalHeight, dFOV);
        float newWidth = originalWidth * ratio;
        float newHeight = originalHeight * ratio;

        return (newWidth, newHeight, xPosition, yPosition);
    }

    public static float CalculateScaleFactor(
        float x, float y, float distance, float originalWidth, float originalHeight, float diagonalFOV)
    {
        float diagonalFOVRad = diagonalFOV * Mathf.Deg2Rad;
        float cosDiagonalFOV = Mathf.Cos(diagonalFOVRad);

        float ratioMin = 1f;
        float ratioMax = 3f;
        float tolerance = 0.0001f;
        int maxIterations = 100;

        float ratio = 1f;
        bool success = false;

        for (int i = 0; i < maxIterations; i++)
        {
            ratio = (ratioMin + ratioMax) / 2f;

            float Ax = x - ratio * originalWidth / 2f;
            float Ay = y - ratio * originalHeight / 2f;
            float Cx = x + ratio * originalWidth / 2f;
            float Cy = y + ratio * originalHeight / 2f;

            float OA_length = Mathf.Sqrt(Ax * Ax + Ay * Ay + distance * distance);
            float OC_length = Mathf.Sqrt(Cx * Cx + Cy * Cy + distance * distance);

            float dotProduct = Ax * Cx + Ay * Cy + distance * distance;

            float cosCurrent = dotProduct / (OA_length * OC_length);

            float difference = cosDiagonalFOV - cosCurrent;

            if (Mathf.Abs(difference) < tolerance)
            {
                success = true;
                Debug.Log("New dFOV: " + Mathf.Acos(cosCurrent) * Mathf.Rad2Deg);
                break;
            }
            else if (difference < 0)
            {
                ratioMin = ratio;
            }
            else
            {
                ratioMax = ratio;
            }
        }

        if (!success)
        {
            Debug.Log("Didn't find suitable ratio");
            Debug.Log("Ratio = " + ratio);
        }
        return ratio;
    }
}
