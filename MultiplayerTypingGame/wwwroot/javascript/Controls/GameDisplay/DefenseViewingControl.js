import { GameTimer } from "./GameTimer.js";

export class DefenseViewingControl extends HTMLElement
{
    // ELEMENTS.
    #phraseDisplaySpan = null;
    #phraseTextbox = null;
    #defenseTimer = null;

    // PRIVATE MEMBERS.
    #timeToTypePhraseInSeconds = 10;
    #typingStartDateTime = null;
    #currentChunk = null;

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
    <span style="font-size: 20px;">Oponent Is Defending.</span>
    <span id="PhraseDisplaySpan"></span>
    <div>
        <game-timer id="GameTimer"></game-timer><input id="PhraseTextbox" type="text"></input>
    </div>
</div>
        `;
        this.#phraseDisplaySpan = shadowRoot.getElementById("PhraseDisplaySpan");
        this.#defenseTimer = shadowRoot.getElementById("GameTimer");
        this.#phraseTextbox = shadowRoot.getElementById("PhraseTextbox");

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

        #PhraseDisplaySpan > span
        {
            font-size: 24px;
        }

        #PhraseTextbox
        {
            height: 20px;
            font-size: 18px;
        }
        `;
        shadowRoot.appendChild(style);
    }

    // PUBLIC FUNCTIONS.
    // Opens the control.
    Open(phrase){
        // SHOW THE CONTROL.
        this.style.display = "block";

        // DISABLE THE TEXTBOX.
        this.#phraseTextbox.disabled = true;

        // ADD THE WORDS TO THE TYPE DISPLAY.
        this.#phraseDisplaySpan.innerHTML = "";
        const phraseChunks = phrase.split(" ");
        phraseChunks.forEach((chunk, index) =>
        {
            // CREATE THE SPAN FOR EACH CHUNK.
            const chunkSpan = document.createElement("span");
            this.#phraseDisplaySpan.insertAdjacentElement("beforeend", chunkSpan);
            chunkSpan.innerText = chunk;
            
            // Add a space to the end of the chunk if it is not the last chunk.
            const chunkIsLastInPhrase = (index == phraseChunks.length - 1);
            if (!chunkIsLastInPhrase)
            {
                chunkSpan.innerText += " ";
            }
        });
        this.#currentChunk = this.#phraseDisplaySpan.children[0];
        this.#currentChunk.style.textDecoration = "underline";
    }

    // Allow the user to defend.
    DisplayPendingDefense(currentDefenseWord)
    {
        this.#startTypingTimer();
        this.#phraseTextbox.value = currentDefenseWord;

        // Move to the next word if the chunk was entered correctly.
        const currentChunkText = this.#currentChunk.innerText;
        const wordIsCorrect = (currentChunkText == currentDefenseWord)
        if (wordIsCorrect)
        {
            this.#currentChunk.style.textDecoration = "none";
            this.#phraseTextbox.value = "";
            this.#currentChunk = this.#currentChunk.nextElementSibling;

            // Check if the full phrase was entered.
            const fullPhraseEntered = this.#currentChunk === null;
            if (fullPhraseEntered)
            {
                // End the typing timer.
                this.#defenseTimer.EndTimer();

                // Close the control.
                this.Close();
            }
            else
            {
                this.#currentChunk.style.textDecoration = "underline";
            }
        }
    }

    // Closes the control.
    Close()
    {
        // HIDE THE CONTROL.
        this.style.display = "none";

        // RESET THE CONTROL.
        this.#timeToTypePhraseInSeconds = 10;
        this.#typingStartDateTime = null;
        this.#phraseTextbox.value = "";
        this.#currentChunk = null;
        this.#phraseDisplaySpan.innerHTML = "";
    }

    // PRIVATE FUNCITONS.
    // Starts the typing timer.
    #startTypingTimer()
    {
        // Check if the typing timer was already started.
        const typingTimerStarted = this.#typingStartDateTime != null;
        if (!typingTimerStarted)
        {
            // Start the timer.
            this.#typingStartDateTime = new Date();
            this.#defenseTimer.StartTimer(this.#timeToTypePhraseInSeconds);
        }
    }
}
customElements.define("defense-viewing-control", DefenseViewingControl);
