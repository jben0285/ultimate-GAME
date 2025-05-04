using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerSelector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public int currentPlayerType = 0;
    [SerializeField]
    List<GameObject> CharacterOptions = new();
    public void showNextCharacter()
    {
        int previousPlayerType = currentPlayerType;
        if (currentPlayerType == CharacterOptions.Count - 1)
        {
            currentPlayerType = 0;
        }
        else
        {
            currentPlayerType++;
        }

        CharacterOptions[previousPlayerType].SetActive(false);
        CharacterOptions[currentPlayerType].SetActive(true);
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        // _img.sprite = _pressed;

    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // _img.sprite = _default;

    }
}
