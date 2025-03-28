import { AttackControl } from "./AttackControl.js";
import { DefenseControl } from "./DefenseControl.js";

export class GameDisplay extends HTMLElement
{
    // ELEMENTS.
    #attackControl = null;
    #defenseControl = null;
    #healthSpan = null;
    
    // CONSTRUCTOR.
    constructor()
    {
        super();
    }

    // Called when connected.
    connectedCallback()
    {
        // CREATE THE SHADOW DOM.
        const shadowRoot = this.attachShadow({ mode: "open" });

        // SET THE INNER HTML.
        shadowRoot.innerHTML = //html
        `
<div id="Display">
    <span style="font-size: 20px;">Health: <span id="HealthSpan" style="color: red;"></span></span>
</div>
<img id="Monkey" src="monkey.gif"/>
<div id="Controls">
    <attack-control id="AttackControl" style="display: none;"></attack-control>
    <defense-control id="DefenseControl" style="display: none;"></defense-control>
</div>
        `;
        this.#attackControl = shadowRoot.getElementById("AttackControl");
        this.#defenseControl = shadowRoot.getElementById("DefenseControl");
        this.#healthSpan = shadowRoot.getElementById("HealthSpan");

        // SET THE STYLE.
        // Set the style of the container.
        this.style.flexDirection = "column";

        // Set the style of the contained elements.
        const style = document.createElement("style");
        style.textContent = //css
        `
        #Display
        {
            height: 50%; 
        }

        #Monkey
        {
            max-width: 30%;
            margin: auto;
        }

        #Controls
        {
            display: flex;
            width: 100%;
            height: 50%;
            justify-content: center;
        }

        @media screen and (max-width: 800px)
        {
            #Display
            {
                height: 50%; 
            }

            #Monkey
            {
                max-width: 50%;
                margin: auto;
            }

            #Controls
            {
                display: flex;
                width: 100%;
                height: 50%;
                justify-content: center;
            }
        }
        `;
        shadowRoot.appendChild(style);
    }
    
    // PUBLIC FUNCTIONS.
    // Opens the control.
    Open()
    {
        // SHOW THE CONTROL.
        this.style.display = "flex";
        this.#healthSpan.innerText = "100";
    }
    
    // Closes the control.
    Close()
    {
        // CLOSE THE CONTROL.
        this.style.display = "none";
    }

    // Allow the user to attack.
    async Attack(socket)
    {
        this.#defenseControl.Close();
        return await this.#attackControl.Open(socket);
    }
    
    // Display the pending phrase to the defender.
    DisplayPendingPhrase(phrase)
    {
        this.#defenseControl.Open();
        this.#defenseControl.DisplayPendingPhrase(phrase);
    }

    // Allow the user to defend.
    async Defend(phrase)
    {
        return await this.#defenseControl.Defend(phrase);
    }

    ShowResult(result)
    {
        this.#healthSpan.innerText = result.health;
    }
}
customElements.define("game-display", GameDisplay);