using UnityEngine;

public class ACLMessage
{
    public string Performative;
    public GameObject Sender;
    public GameObject Receiver;
    public string Content;
    public string Protocol;
    public string Ontology;

    public ACLMessage(string performative, GameObject sender, GameObject receiver,
        string content, string protocol, string ontology)
    {
        Performative = performative;
        Sender = sender;
        Receiver = receiver;
        Content = content;
        Protocol = protocol;
        Ontology = ontology;
    }
}