using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] GameObject loadingObject;
    [SerializeField] Animator animator;
    [SerializeField] float transitionTime = 1f;

    public void SceneChange(string name)
    { 
        StartCoroutine(LoadScene(name));
    }


    IEnumerator LoadScene(string name)
    {
        //loadingObject.gameObject.SetActive(true);
        animator.SetTrigger("Start");

        yield return new WaitForSeconds(transitionTime);

        SceneManager.LoadScene(name);
    }
}
