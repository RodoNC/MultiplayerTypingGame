import { AttackControl } from "./AttackControl.js";
import { DefenseControl } from "./DefenseControl.js";

export class GameWindow extends HTMLElement
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
        shadowRoot.innerHTML = `
<div id="Display">
    <span style="font-size: 20px;">Health: <span id="HealthSpan" style="color: red;">100</span></span>
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
        style.textContent = `
            #Display
            {
                height: 50%; 
            }

            #Monkey
            {
                height: 60%;
                width: 30%;
                margin: auto;
            }

            #Controls
            {
                display: flex;
                width: 100%;
                height: 50%;
                justify-content: center;
            }
        `;
        shadowRoot.appendChild(style);
    }
    
    // PUBLIC FUNCTIONS.
    // Opens the window.
    Open()
    {
        // SHOW THE WINDOW.
        this.style.display = "flex";
    }
    
    // Closes the window.
    Close()
    {
        // CLOSE THE WINDOW.
        this.style.display = "none";
    }

    // Allow the user to attack.
    async Attack()
    {
        return await this.#attackControl.Open()
    }
    
    // Allow the user to defend.
    async Defend(phrase)
    {
        return await this.#defenseControl.Open(phrase)
    }

    ShowResult(result)
    {
        this.#healthSpan.innerText = result.health;
    }
}
customElements.define("game-window", GameWindow);