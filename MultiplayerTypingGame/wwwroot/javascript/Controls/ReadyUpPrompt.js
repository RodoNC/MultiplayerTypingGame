export class ReadyUpPrompt extends HTMLElement
{
    // ELEMENTS.
    #readiedUpMessage = null;
    #readyUpButton = null;

    // PRIVATE MEMBERS.
    #readyUpPromiseResolver = null;

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
<div id="Container">
    <span id="ReadiedUpMessage" style="display: none; font-size: 20px;">Readied up. Waiting for opponent.</span>
    <button id="ReadyUpButton">Ready Up</button>
</div>
        `;
        this.#readiedUpMessage = shadowRoot.getElementById("ReadiedUpMessage");
        this.#readyUpButton = shadowRoot.getElementById("ReadyUpButton");

        // SET THE STYLE.
        const style = document.createElement("style");
        style.textContent = //css
        `
        #Container
        {
            display: flex;
            flex-direction: column;
            align-items: center;
            row-gap: 10px;
        }
        
        #ReadyUpButton
        {
            height: 20px;
            font-size: 18px;
        }
        `;
        shadowRoot.appendChild(style);

        // HANDLE THE USER STARTING TO TYPE.
        this.#readyUpButton.addEventListener("click", () =>
        {
            this.#readyUpButton.style.display = "none";
            this.#readiedUpMessage.style.display = "block";
            this.#readyUpPromiseResolver({ type: "readyUp" } );
        });
    }
    
    // PUBLIC FUNCTIONS.
    // Opens the control.
    async PromptPlayer()
    {
        // RESET THE CONTROL.
        this.#readyUpButton.style.display = "block";
        this.#readiedUpMessage.style.display = "none";
        
        // SHOW THE CONTROL.
        this.style.display = "block";

        // CREATE A PROMISE TO BE RESOLVED WHEN THE TIME HAS ENDED
        // OR A PHRASE WAS SUBMITTED.
        const readyUpPromise = new Promise((resolve) =>
            {
                this.#readyUpPromiseResolver = resolve;
            });
        return readyUpPromise;
    }

    // Closes the control.
    Close()
    {
        // RESOLVE THE PROMISE.
        // This is needed since the server is waiting on the player to ready up.
        this.#readyUpPromiseResolver({ type: "readyUp" } );

        // HIDE THE CONTROL.
        this.style.display = "none";
    }
    
    // PRIVATE FUNCTIONS.
}
customElements.define("ready-up-prompt", ReadyUpPrompt);