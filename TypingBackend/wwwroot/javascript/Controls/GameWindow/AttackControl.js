import { GameTimer } from "./GameTimer.js";

export class AttackControl extends HTMLElement
{
    // ELEMENTS.
    #phraseTextbox = null;
    #attackTimer = null;

    // PRIVATE MEMBERS.
    #gracePeriodInSeconds = 5;
    #timeToTypePhraseInSeconds = 10;
    #typingStartDateTime = null;
    #attackPromiseResolver = null;
    #socket = null;
    
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
<div id="Container">
    <span style="font-size: 20px;">Attack! Type in a phrase.</span>
    <div>
        <game-timer id="GameTimer"></game-timer><input id="PhraseTextbox" type="text"></input>
    </div>
</div>
        `;
        this.#attackTimer = shadowRoot.getElementById("GameTimer");
        this.#phraseTextbox = shadowRoot.getElementById("PhraseTextbox");

        // SET THE STYLE.
        const style = document.createElement("style");
        style.textContent = `
        #Container
        {
            display: flex;
            flex-direction: column;
            align-items: center;
            row-gap: 10px;
        }
        
        #PhraseTextbox
        {
            height: 20px;
            font-size: 18px;
        }
        `;
        shadowRoot.appendChild(style);

        // HANDLE THE USER STARTING TO TYPE.
        this.#phraseTextbox.addEventListener("input", () =>
        {
            const startedTyping = this.#typingStartDateTime != null;
            if (!startedTyping)
            {
                // End the grace period timer.
                this.#attackTimer.EndTimer();
            }
        });

        // HANDLE THE USER SUBMITTING THE PHRASE.
        this.#phraseTextbox.addEventListener("keydown", (event) =>
        {
            if (event.key === "Enter")
            {
                // End the typing timer.
                this.#attackTimer.EndTimer();
            }
        });
        
        // HANDLE THE USER ENTERING CHARACTERS.
        this.#phraseTextbox.addEventListener("keypress", (event) =>
        {
            // Only allow alphabet and space characters.
            const characterIsAlpha = event.code === `Key${event.key.toUpperCase()}`;
            const characterIsSpace = event.code === "Space";
            if (!characterIsAlpha && !characterIsSpace)
            {
                event.preventDefault();
            }
        });
        
        // SEND THE PENDING MESSAGE TO THE BACKEND.
        this.#phraseTextbox.addEventListener("change", (event) =>
        {
            this.#socket.send(JSON.stringify(
            {
                type: "pendingPhrase",
                phrase: this.#phraseTextbox.value.trim()
            }));
        });
    }
    
    // PUBLIC FUNCTIONS.
    // Opens the control.
    async Open(socket)
    {
        // SET THE SOCKET.
        this.#socket = socket;

        // SHOW THE CONTROL.
        this.style.display = "block";

        // START THE GRACE PERIOD TIMER.
        this.#attackTimer.StartTimer(this.#gracePeriodInSeconds).then(() =>
            {
                this.#startTypingTimer();
            });
            
        // FOCUS ON THE TEXTBOX.
        this.#phraseTextbox.focus();
        
        // CREATE A PROMISE TO BE RESOLVED WHEN THE TIME HAS ENDED
        // OR A PHRASE WAS SUBMITTED.
        const attackPromise = new Promise((resolve) =>
            {
                this.#attackPromiseResolver = resolve;
            });
        return attackPromise;
    }

    // Closes the control.
    #close()
    {
        // RESET THE CONTROL.
        this.#gracePeriodInSeconds = 5;
        this.#timeToTypePhraseInSeconds = 10;
        this.#typingStartDateTime = null;
        this.#attackPromiseResolver = null;
        this.#phraseTextbox.value = "";
        
        // HIDE THE CONTROL.
        this.style.display = "none";
    }
    
    // PRIVATE FUNCTIONS.
    // Starts the typing timer.
    #startTypingTimer()
    {
        // Check if the timer was already started.
        const typingTimerStarted = this.#typingStartDateTime != null;
        if (!typingTimerStarted)
        {
            // Start the timer.
            this.#typingStartDateTime = new Date();
            this.#attackTimer.StartTimer(this.#timeToTypePhraseInSeconds).then(() =>
            {
                // Submit the phrase when time runs out.
                const typingEndDateTime = new Date();
                const typeTimeInSeconds = (typingEndDateTime - this.#typingStartDateTime) / 1000;
                this.#attackPromiseResolver(
                    {
                        phrase: this.#phraseTextbox.value.trim(),
                        time: typeTimeInSeconds
                    });
                this.#close();
            });
        }
    }
}
customElements.define("attack-control", AttackControl);