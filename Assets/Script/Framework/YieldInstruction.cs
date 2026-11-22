using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal static class YieldInstruction
{
    //GC 호출을 최대한 줄이기 위해 미리 선언
    public static readonly WaitForEndOfFrame WaitForEndOfFrame = new WaitForEndOfFrame();
    public static readonly WaitForFixedUpdate WaitForFixedUpdate = new WaitForFixedUpdate();
    public static readonly WaitForSeconds waitFor1Seconds = new WaitForSeconds(1.0f);
    public static readonly WaitForSeconds waitForhalfSeconds = new WaitForSeconds(0.5f);
}