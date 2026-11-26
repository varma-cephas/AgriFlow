import type { BusinessType } from '../types/auth';

export const BUSINESS_TYPES: BusinessType[] = [
  'Supermarket',
  'Hotel',
  'Restaurant',
  'School',
  'Hospital',
  'Processor',
  'Exporter',
  'Other',
];

export const RWANDA_CITIES: string[] = [
  'Kigali',
  'Nyagatare',
  'Rwamagana',
  'Kayonza',
  'Musanze',
  'Rubavu',
  'Huye',
  'Muhanga',
  'Rusizi',
  'Karongi',
];

export type RwandaLocationTree = Record<
  string, // Province
  Record<
    string, // District
    Record<
      string, // Sector
      string[] // Cells
    >
  >
>;

export const RWANDA_LOCATIONS: RwandaLocationTree = {
  'Eastern Province': {
    Nyagatare: {
      Rukomo: ['Gahurura', 'Nyakagarama', 'Rukomo II', 'Rurenge'],
      Nyagatare: ['Barija', 'Cyabayaga', 'Nsheke', 'Ryabega'],
      Karangazi: ['Kama', 'Musenyi', 'Ndama', 'Rwisirabo'],
      Mimuli: ['Gikagati', 'Mahoro', 'Rugari'],
    },
    Rwamagana: {
      Kigabiro: ['Cyanya', 'Nyagasenyi', 'Sibagire', 'Sovu'],
      Muhazi: ['Byimana', 'Kabare', 'Kitazigurwa', 'Murambi'],
      Mwulire: ['Bicumbi', 'Bushenyi', 'Ntunga'],
    },
    Kayonza: {
      Mukarange: ['Bwiza', 'Kayonza', 'Nyagatovu', 'Rugendabari'],
      Kabare: ['Cyarubare', 'Gitara', 'Rubimba'],
    },
    Bugesera: {
      Nyamata: ['Kanazi', 'Kayumba', 'Maranyundo', 'Murama'],
      Rilima: ['Kabeza', 'Karera', 'Kimaranzara'],
    },
  },
  'Kigali City': {
    Gasabo: {
      Kimironko: ['Bibare', 'Kibagabaga', 'Nyagatovu'],
      Remera: ['Nyabisindu', 'Nyarutarama', 'Rukiri I', 'Rukiri II'],
      Kinyinya: ['Gacuriro', 'Gasharu', 'Kagugu', 'Murama'],
    },
    Kicukiro: {
      Niboye: ['Gatare', 'Niboye', 'Nyakabanda'],
      Kanombe: ['Busanza', 'Kabeza', 'Karama', 'Rubirizi'],
      Gahanga: ['Gahanga', 'Kagasa', 'Karembure'],
    },
    Nyarugenge: {
      Nyarugenge: ['Agatare', 'Biryogo', 'Kiyovu', 'Rwampara'],
      Muhima: ['Amahoro', 'Kabasengerezi', 'Nyabugogo', 'Rugenge'],
    },
  },
  'Northern Province': {
    Musanze: {
      Muhoza: ['Cyabararika', 'Kigombe', 'Mpenge', 'Ruhengeri'],
      Kinigi: ['Bisoke', 'Kaguhu', 'Nyonirima'],
    },
    Rulindo: {
      Base: ['Cyohoha', 'Gitare', 'Rwamahwa'],
      Bushoki: ['Gasiza', 'Kayenzi', 'Mukoto'],
    },
  },
  'Southern Province': {
    Huye: {
      Ngoma: ['Butare', 'Kaburemera', 'Matyazo', 'Ngoma'],
      Tumba: ['Cyarwa', 'Cyimana', 'Gitwa', 'Mpare'],
    },
    Muhanga: {
      Nyamabuye: ['Gahogo', 'Gifumba', 'Gitarama', 'Remera'],
    },
  },
  'Western Province': {
    Rubavu: {
      Gisenyi: ['Amahoro', 'Bugoyi', 'Kivumu', 'Nengo'],
      Nyamyumba: ['Burushya', 'Kinigi', 'Rubona'],
    },
    Rusizi: {
      Kamembe: ['Cyangugu', 'Gihundwe', 'Kamashangi'],
    },
  },
};