export class GameTimer extends HTMLElement
{
    // ELEMENTS.
    #timerSpan = null;

    // PRIVATE MEMBERS.
    #timeLeftInTimer = 0;
    #timerIntervalId = null;
    #timerPromiseResolver;

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

        // CREATE THE SPAN TO DISPLAY THE TIME LEFT.
        this.#timerSpan = document.createElement("span");
        shadowRoot.appendChild(this.#timerSpan);
        this.#timerSpan.innerText = 0;

        // SET THE STYLE.
        const style = document.createElement("style");
        style.textContent = `
span
{
    border: 2px solid black;
    font-size: 40px;
    padding: 1px 3px 1px;
    margin-right: 10px;
}`;
        shadowRoot.appendChild(style);
    }

    // PUBLIC METHODS.
    // Starts the timer.
    async StartTimer(time) 
    {
        // END THE TIMER.
        this.EndTimer();

        // START THE TIMER.
        this.#timeLeftInTimer = time;

        // Update the timer display.
        this.#timerSpan.innerText = this.#timeLeftInTimer;
        this.#timerIntervalId = setInterval(() =>
        {
            // Update the timer display.
            this.#timeLeftInTimer -= 1;
            this.#timerSpan.innerText = this.#timeLeftInTimer;

            // Check if there is no time left.
            const timeIsLeft = this.#timeLeftInTimer <= 0;
            if (timeIsLeft)
            {
                this.EndTimer()
            }      
        },
        1000);

        // Set the timer.
        const timeInMilliseconds = time * 1000;
        return new Promise((resolve) =>
        {
            this.#timerPromiseResolver = resolve;
            setTimeout(
                resolve,
                timeInMilliseconds);
        });
    }

    // Ends the timer.
    EndTimer()
    {
        const timerStarted = this.#timerIntervalId != null;
        if (timerStarted)
        {
            clearInterval(this.#timerIntervalId);
            this.#timerIntervalId = null;
            this.#timerPromiseResolver();
        }
    }
}
customElements.define("game-timer", GameTimer);
