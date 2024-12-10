getImports = async () =>
{
    const { AttackWindow } = await import("./Controls/AttackWindow.js");
    const { DefenseWindow } = await import("./Controls/DefenseWindow.js");
}

let socket = null;
// ON LOAD FUNCTION.    
onload = async () =>
{
    await getImports();

    // HANDLE THE USER CREATING A ROOM.
    const createRoomButton =  document.getElementById("CreateRoomButton");
    createRoomButton.addEventListener("click", () =>
    {
        const createRoomUrl = new URL("/createRoom", window.location.href);
        socket = new WebSocket(createRoomUrl.href);
        socket.onclose = () =>
        {
            console.log("websocket closed.");
        };
        startGame(socket).then((reason) =>
        {
            console.log(reason)
            // GO BACK TO THE MAIN MENU.
            const attackWindow = document.getElementById("AttackWindow").Close();
            const defenseWindow = document.getElementById("DefenseWindow").Close();
            const mainMenu = document.getElementById("MainMenu");
            mainMenu.style.display = "flex";
        });
    });

    // HANDLE THE USER JOINING A ROOM.
    const joinRoomTextbox =  document.getElementById("JoinRoomTextbox");
    joinRoomTextbox.addEventListener("keypress", (event) =>
    {
        if (event.key == "Enter")
        {
            const joinRoomUrl = new URL("/joinRoom", window.location.href);
            joinRoomUrl.searchParams.append("roomKey", joinRoomTextbox.value.trim());
            socket = new WebSocket(joinRoomUrl.href);
            socket.onclose = () =>
            {
                console.log("websocket closed.");
            };
            startGame(socket).then((reason) =>
            {
                console.log(reason)
                // GO BACK TO THE MAIN MENU.
                const attackWindow = document.getElementById("AttackWindow").Close();
                const defenseWindow = document.getElementById("DefenseWindow").Close();
                const mainMenu = document.getElementById("MainMenu");
                mainMenu.style.display = "flex";
            });
        } 
    });
}

startGame = async (socket) =>
{
    // HIDE THE MAIN MENU.
    const mainMenu = document.getElementById("MainMenu");
    mainMenu.style.display = "none";

    // HANDLE THE SOCKET DISCONNECTING.
    let gameEndPromiseResolver;
    socket.onclose = () => { gameEndPromiseResolver("The connection was closed by the server."); };
    socket.onerror = () => { gameEndPromiseResolver("The connection was closed due to a connection error."); };

    // HANDLE MESSAGES FROM THE WEBSOCKET CONNECTION.
    const attackWindow = document.getElementById("AttackWindow");
    const defenseWindow = document.getElementById("DefenseWindow");
    socket.onmessage = (event) =>
    {
        const message = JSON.parse(event.data);
        switch (message.type)
        {
            case "created":
            {
                // Show the room key.
                const roomKeySpan = document.getElementById("RoomKeySpan");
                roomKeySpan.style.display = "block";
                const roomKeyValueSpan = document.getElementById("RoomKeyValueSpan");
                roomKeyValueSpan.innerText = message.roomKey;
                break;
            }
            case "start":
            {
                // Hide the room key.
                const roomKeySpan = document.getElementById("RoomKeySpan");
                roomKeySpan.style.display = "none";

                // Reset the health.
                const healthSpan = document.getElementById("HealthSpan");
                healthSpan.innerText = 100;

                break;
            }
            case "attack":
            {
                attackWindow.Open().then((res) =>
                {
                    attackWindow.Close();
                    socket.send(JSON.stringify(res));
                });
                break;
            }
            case "defend":
            {
                defenseWindow.Open(message.phrase).then((res) =>
                {
                    defenseWindow.Close();
                    socket.send(JSON.stringify(res));
                });
                break;
            }
            case "result":
            {
                // Update the health.
                const healthSpan = document.getElementById("HealthSpan");
                healthSpan.innerText = message.health;

                // Display the message.
                console.log(message.resultMessage);
                break;
            }
            default:
                gameEndPromiseResolver("Game ended due to unknown reason.");
                break;
        }
    }

    // CREATE THE PROMISE TO BE RESOLVED WHEN THE GAME ENDS.
    return new Promise((resolve) =>
    {
        gameEndPromiseResolver = resolve;
    });
}
