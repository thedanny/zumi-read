import type { ReaderData } from './ReaderView'

const readerDefaults = {
  questionCount: 3,
  showChildQuestions: true,
  lockStoryDuringQuestions: false,
  questionModeStarted: false,
  questionsCompleted: false,
}

export const sampleStories: ReaderData[] = [
  {
    ...readerDefaults,
    title: 'Mira and the Green Leaf Box',
    episodeNumber: 1,
    paragraphs: [
      { id: 'p1', text: 'On Saturday morning, Mira visited a neighborhood fair with her grandfather. Near a crooked fence, she noticed a small wooden box with a painted green leaf on its lid. Inside was a note asking anyone who found it to help a neighbor. Mira wondered who had left the box and why. She put the note back carefully and looked around for a clue.' },
      { id: 'p2', text: 'A trail of tiny blue ribbons led Mira past a bench to Mrs. Vale’s porch. Mrs. Vale was carrying pots of seedlings, but the wind had scattered their labels. Without the labels, she could not tell which plants needed shade. Mira offered to help. Grandfather suggested looking at the leaf shapes before making a guess.' },
      { id: 'p3', text: 'Mira studied each plant instead of rushing. The broad leaves matched the drawing on one fallen label, while the narrow leaves matched another. She and Mrs. Vale placed the labels beside the right pots. One ribbon had caught on a watering can, so Mira tied it back to the handle. Now the trail made sense: Mrs. Vale had left it for a willing helper.' },
      { id: 'p4', text: 'When the pots were safe, Mrs. Vale smiled and gave Mira a paper packet of seeds. Mira asked if they could plant them together next week. Grandfather agreed. On the walk home, Mira realized that the mysterious box had led her to something better than treasure: a new friendship and a useful job well done.' },
    ],
    questions: [
      { id: 'q1', text: 'What clues helped Mira find Mrs. Vale?' },
      { id: 'q2', text: 'Why did Mira look at the leaves before moving the labels?' },
      { id: 'q3', text: 'What do you think Mira might do with the seeds next week?' },
    ],
  },
  {
    ...readerDefaults,
    title: 'Mira and the Lantern Path',
    episodeNumber: 2,
    paragraphs: [
      { id: 'p1', text: 'A week after meeting Mrs. Vale, Mira and Grandfather carried the new seedlings to the riverside festival. Paper lanterns hung above the path, and the air smelled of warm bread. Mrs. Vale was waiting near the community garden. She pointed to a small sign: “Follow the lanterns to the evening story circle.” Mira could hardly wait to hear the first tale.' },
      { id: 'p2', text: 'Before they reached the circle, a gust of wind blew out two lanterns. Families coming from the bridge stopped at a fork in the path. One trail led toward the garden; the other ended beside a locked shed. Mira noticed fresh footprints going toward the garden, but they vanished where the ground turned stony. She did not want to send anyone the wrong way.' },
      { id: 'p3', text: 'Grandfather asked what else could mark the route. Mira listened. Through the trees came the soft sound of someone reading aloud. She also spotted a ribbon tied to a branch, the same blue as Mrs. Vale’s ribbons. Mira checked that the voice grew louder along that trail. Then she guided the waiting families around the bend, where a circle of cushions sat beneath glowing lanterns.' },
      { id: 'p4', text: 'Mrs. Vale thanked Mira for using more than one clue. Together they planted the seedlings at the edge of the garden so visitors could watch them grow. When the story circle began, Mira settled beside Grandfather. She liked that a careful choice had helped everyone find their way, and she wondered what surprise the next adventure would bring.' },
    ],
    questions: [
      { id: 'q1', text: 'Why did the families stop at the fork in the path?' },
      { id: 'q2', text: 'Which two clues made Mira confident about the right trail?' },
      { id: 'q3', text: 'What does Mira’s choice tell you about how she solves problems?' },
    ],
  },
  {
    ...readerDefaults,
    title: 'Mira and the Museum Map',
    episodeNumber: 3,
    paragraphs: [
      { id: 'p1', text: 'On a rainy afternoon, Mira met Mrs. Vale and Grandfather at the little town museum. A new exhibit showed how people had traveled before cars filled the streets. Mira hoped to see the model boat on the poster. At the entrance, a volunteer handed them a folded map, but a splash from an umbrella had blurred the room names.' },
      { id: 'p2', text: 'Grandfather suggested asking the volunteer for a fresh map. The volunteer had given away the last one, so Mira looked for signs around the hallway. A painted wheel pointed toward a room of bicycles. A tiny train carved into the wall led to the railway display. Neither seemed to match the boat on the poster. Mira turned the damp map over and found a small drawing of an anchor on its back.' },
      { id: 'p3', text: 'She followed the anchor marks along the floor, pausing whenever the trail crossed another symbol. At the far end of the museum, the marks stopped beside a blue door. Inside stood the model boat, its sails made of thin cotton. A card explained that neighbors had once used boats to carry food across the river when the bridge was flooded.' },
      { id: 'p4', text: 'Mrs. Vale noticed that Mira had studied the map rather than giving up on it. Mira smiled and said the smeared names had made the pictures more useful. Before leaving, she drew a clear route on the back of the map for the next family. Grandfather tucked it beside the door, and Mira took one last look at the little boat.' },
    ],
    questions: [
      { id: 'q1', text: 'What happened to the room names on the museum map?' },
      { id: 'q2', text: 'Why did Mira follow the anchor marks instead of the wheel or train?' },
      { id: 'q3', text: 'How did Mira make the museum visit easier for another family?' },
    ],
  },
  {
    ...readerDefaults,
    title: 'Mira and the Hilltop Message',
    episodeNumber: 4,
    paragraphs: [
      { id: 'p1', text: 'The first warm day of spring brought Mira, Grandfather, and Mrs. Vale to a hill above town. They carried a picnic and a small kite shaped like a fox. At the top, Mira found a wooden sign with a faded message: “Please leave the hill as welcoming as you found it.” She read it twice, wondering what that might mean for their visit.' },
      { id: 'p2', text: 'While Grandfather spread the blanket, Mira saw bright scraps of paper scattered near the path. The wind had lifted them from an overflowing bin. She wanted to fly the kite before the breeze disappeared, but a younger child was trying to pick up the scraps alone. Mira asked Mrs. Vale to hold the kite and went to help. Together they gathered the paper in a bag from the picnic basket.' },
      { id: 'p3', text: 'The child pointed toward the sign. “My brother painted those words,” he said. “People think they only mean taking rubbish away.” Mira looked around. Someone had left a narrow gate open, and a family with a stroller could not pass because it blocked the path. She closed the gate gently. Then she and the child moved a fallen branch off the trail.' },
      { id: 'p4', text: 'At last Mira flew the fox kite. It climbed high above the picnic blanket, its orange tail curling in the breeze. Grandfather asked what she thought the sign meant now. “Leave room for the next person to enjoy this place,” Mira said. The hill looked almost the same as when they arrived, but she knew three small choices had made it more welcoming.' },
    ],
    questions: [
      { id: 'q1', text: 'Where had the paper scraps come from?' },
      { id: 'q2', text: 'Why did Mira wait to fly her kite?' },
      { id: 'q3', text: 'What did Mira learn the sign meant beyond picking up rubbish?' },
    ],
  },
]

const storageKey = 'zumiread-next-sample'

export function nextSampleIndex(): number {
  try {
    const stored = Number.parseInt(localStorage.getItem(storageKey) ?? '0', 10)
    const index = Number.isFinite(stored) && stored >= 0 ? stored % sampleStories.length : 0
    localStorage.setItem(storageKey, String((index + 1) % sampleStories.length))
    return index
  } catch {
    return 0
  }
}
