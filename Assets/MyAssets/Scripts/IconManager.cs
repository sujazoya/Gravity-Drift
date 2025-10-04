using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
namespace zoya.game
{
    
    public class IconManager : MonoBehaviour
    {
        public static IconManager Instance { get; private set; }
        [Header("Icons & Indicators")]
        [SerializeField] private GameObject hostIcon;
        [SerializeField] private GameObject readyIcon;
        [SerializeField] private GameObject deadIcon;
        [SerializeField] private GameObject talkingIcon;
        [SerializeField] private GameObject mutedIcon;
        [SerializeField] private Image classIcon;
        [SerializeField] private Image rankIcon;
        public GameObject poisonIcon;
        public GameObject burnIcon;
        public GameObject stunIcon;
        public GameObject slowIcon;
        public GameObject invulnerableIcon;

        [Header("Buff Icons")]
        public GameObject speedBoostIcon;
        public GameObject damageBoostIcon;
        public GameObject defenseBoostIcon;
        public GameObject regenerationIcon;
        private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
            
        }
        else
        {
            Destroy(gameObject);
        }
    }    
      
    }
}
