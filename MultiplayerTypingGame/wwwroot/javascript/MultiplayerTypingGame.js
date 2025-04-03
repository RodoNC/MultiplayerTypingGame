let socket = null;
// ON LOAD FUNCTION.    
onload = async () =>
{
    // POPULATE THE ROOM TABLE.
    populateRoomTable();

    // HANDLE THE USER CREATING A ROOM.
    const createRoomButton =  document.getElementById("CreateRoomButton");
    const createRoomTextbox =  document.getElementById("CreateRoomTextbox");
    createRoomButton.addEventListener("click", () =>
    {
        const createRoomUrl = new URL("/createRoom", window.location.href);
        createRoomUrl.searchParams.append("roomName", createRoomTextbox.value.trim());
        socket = new WebSocket(createRoomUrl.href);
        socket.onclose = () =>
        {
            console.log("websocket closed.");
        };
        startGame(socket);
    });
}

populateRoomTable = async () =>
{
    // RETRIEVE THE ROOMS.
    const getRoomsUrl = new URL("/getRooms", window.location.href);
    let response = await fetch(
        getRoomsUrl.href, 
        {
            headers: {'Accept': 'application/json'}
        });
        let roomsJson = await response.json();
        let rooms = roomsJson;
        
    // CHECK IF THERE ARE ANY ROOMS.
    const anyRooms = rooms !== null && rooms.length !== 0
    if (!anyRooms)
    {
        //document.getElementById("NoRoomsOpenMessage").innerText = "";
        document.getElementById("NoRoomsOpenMessage").style.display = "block";
        return;
    }

    // ADD THE ROOMS TO THE TABLE.
    const roomTableBody = document.getElementById("RoomTable").getElementsByTagName('tbody')[0];
    roomTableBody.innerHTML = "";
    rooms.forEach((room) => {
        // CREATE THE ROW WITH THE ROOM DETAILS.
        const roomRow = roomTableBody.insertRow(-1);
        roomRow.insertCell(-1).innerText = room.RoomName;
        roomRow.insertCell(-1).innerText = `${room.Players.length}/2`;
        
        // JOIN ROOM WHEN CLICKING ON THE ROW.
        // Check if the room is full.
        const roomIsFull = room.Players.length === 2;
        if (roomIsFull)
        {
            return;
        }

        // Make the room appear clickable.
        roomRow.style.cursor = "pointer";
        roomRow.addEventListener('click', () => {

            // Join the room.
            const joinRoomUrl = new URL("/joinRoom", window.location.href);
            joinRoomUrl.searchParams.append("roomName", roomRow.cells[0].innerText.trim());
            socket = new WebSocket(joinRoomUrl.href);
            socket.onclose = () =>
            {
                console.log("websocket closed.");
            };
            startGame(socket);
        });
    });
}

startGame = async (socket) =>
{
    // HIDE THE MAIN MENU.
    const mainMenu = document.getElementById("MainMenu");
    mainMenu.style.display = "none";

    // CREATE THE PROMISE TO BE RESOLVED WHEN THE GAME ENDS.
    let gameEndPromiseResolver ;
    const gameEndPromise = (new Promise((resolve) =>
    {
        gameEndPromiseResolver = resolve;
    })).then(() =>
    {
        window.location.reload();
    });
    
    // HANDLE THE SOCKET DISCONNECTING.
    socket.onclose = () => { gameEndPromiseResolver("The connection was closed by the server."); };
    socket.onerror = () => { gameEndPromiseResolver("The connection was closed due to a connection error."); };
    
    // HANDLE MESSAGES FROM THE WEBSOCKET CONNECTION.
    const gameDisplay = document.getElementById("GameDisplay");
    const readyUpPrompt = document.getElementById("ReadyUpPrompt");
    const gameResultSpan = document.getElementById("GameResultSpan");
    const waitingForOpponentMessageSpan = document.getElementById("WaitingForOpponentMessageSpan");
    socket.onmessage = (event) =>
    {
        const message = JSON.parse(event.data);
        switch (message.type)
        {
            case "created":
            case "joined":
            {
                // Show the room key.
                const roomNameSpan = document.getElementById("RoomNameSpan");
                roomNameSpan.style.display = "block";
                const roomNameValueSpan = document.getElementById("RoomNameValueSpan");
                roomNameValueSpan.innerText = message.roomName;
                break;
            }
            case "waitingForOpponent":
            {
                readyUpPrompt.Close();
                gameDisplay.Close();
                waitingForOpponentMessageSpan.style.display = "block";
                break;
            }
            case "promptReadyUp":
                {
                waitingForOpponentMessageSpan.style.display = "none";
                readyUpPrompt.PromptPlayer().then((response) =>
                {
                    socket.send(JSON.stringify(response));
                });
                break;
            }
            case "start":
            {
                readyUpPrompt.Close();
                gameResultSpan.innerText = "";
                gameDisplay.Open();
                break;
            }
            case "promptAttack":
            {
                gameDisplay.Attack(socket).then((response) =>
                {
                    socket.send(JSON.stringify(response));
                });
                break;
            }
            case "pendingPhrase":
            {
                gameDisplay.DisplayPendingPhrase(message.phrase);
                break;  
            }
            case "pendingDefense":
            {
                gameDisplay.DisplayPendingDefense(message.phrase);
                break;
            }
            case "promptDefense":
            {
                gameDisplay.Defend(message.phrase, socket).then((response) =>
                {
                    socket.send(JSON.stringify(response));
                });
                break;
            }
            case "result":
            {
                gameDisplay.ShowResult(message);                
                break;
            }
            case "opponentDisconnected":
            {
                gameDisplay.Close();
                readyUpPrompt.Close();
                break;
            }
            case "gameEnded":
            {
                gameDisplay.Close();
                gameResultSpan.innerText = message.resultMessage;
                break;
            }
            case "ping":
            {
                const pongMessage = { type: "pong" };
                socket.send(JSON.stringify(pongMessage));
                break;
            }
            default:
            {
                gameEndPromiseResolver("Game ended due to unknown reason.");
                break;
            }
        }
    }

    return gameEndPromise;
}
